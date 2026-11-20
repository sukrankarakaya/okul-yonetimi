import { createOkulClient, type GuardianCodeResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import App from '../../../App';
import { secureTokenStore, type SecureStorage } from '../../auth/secureSession';
import { GuardianPhoneScreen } from './GuardianPhoneScreen';

/** Spec 0007, AC-1 / AC-3 / AC-9 — Veli Girişi on mobile, and the whole veli flow over the real api-client. */

function memoryStorage(): SecureStorage & { items: Map<string, string> } {
  const items = new Map<string, string>();
  return {
    items,
    getItemAsync: async (key) => items.get(key) ?? null,
    setItemAsync: async (key, value) => void items.set(key, value),
    deleteItemAsync: async (key) => void items.delete(key),
  };
}

function json(status: number, body?: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('GuardianPhoneScreen', () => {
  let announce: jest.SpyInstance;
  beforeEach(() => {
    announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
  });
  afterEach(() => announce.mockRestore());

  it('sends kurum kodu + telefon from a phone keypad and reports the trimmed kurum kodu', async () => {
    const request = jest.fn(async (): Promise<GuardianCodeResult> => ({ kind: 'sent' }));
    const onSent = jest.fn();
    await render(<GuardianPhoneScreen request={request} onSent={onSent} />);

    expect(screen.getByRole('header', { name: 'Veli Girişi' })).toBeOnTheScreen();
    expect(screen.getByLabelText('Telefon numarası').props.keyboardType).toBe('phone-pad');
    await fireEvent.changeText(screen.getByLabelText('Kurum kodu'), ' ATATURK-IST ');
    await fireEvent.changeText(screen.getByLabelText('Telefon numarası'), '0532 123 45 67');
    await fireEvent.press(screen.getByRole('button', { name: 'SMS kodu gönder' }));

    await waitFor(() => expect(onSent).toHaveBeenCalledWith('ATATURK-IST', '0532 123 45 67'));
  });

  it('marks empty fields "Bu alan gerekli." and announces it', async () => {
    const request = jest.fn();
    await render(<GuardianPhoneScreen request={request} onSent={jest.fn()} />);

    await fireEvent.press(screen.getByRole('button', { name: 'SMS kodu gönder' }));

    expect(screen.getAllByText('Bu alan gerekli.')).toHaveLength(2);
    expect(announce).toHaveBeenCalledWith('Bu alan gerekli.');
    expect(request).not.toHaveBeenCalled();
  });

  it.each([
    [{ kind: 'invalidPhone' } as const, 'Lütfen geçerli bir telefon numarası girin.'],
    [{ kind: 'rateLimited' } as const, 'Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.'],
    [{ kind: 'smsFailed' } as const, 'SMS gönderilemedi. Tekrar deneyin.'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows the text of %o', async (result, text) => {
    await render(<GuardianPhoneScreen request={async () => result} onSent={jest.fn()} />);

    await fireEvent.changeText(screen.getByLabelText('Kurum kodu'), 'ATATURK-IST');
    await fireEvent.changeText(screen.getByLabelText('Telefon numarası'), '0532');
    await fireEvent.press(screen.getByRole('button', { name: 'SMS kodu gönder' }));

    expect(await screen.findByText(text)).toBeOnTheScreen();
    expect(announce).toHaveBeenCalledWith(text);
  });

  it('the app: Giriş → Veli girişi → kod → KVKK → Kurum bilgisi; kurum kodu remembered, telefon never; tokens in the secure store', async () => {
    const storage = memoryStorage();
    const hints = memoryStorage();
    const responses: Record<string, Response[]> = {
      'POST /api/v1/auth/guardian/code': [json(200, { message: 'x' })],
      'POST /api/v1/auth/guardian/verify': [
        json(200, {
          session: { roles: ['Guardian'], accessExpiresAt: 'x', sessionExpiresAt: 'y', accessToken: 'veli-access', refreshToken: 'veli-refresh' },
          consentRequired: true,
        }),
      ],
      'GET /api/v1/guardian/consent': [json(200, { accepted: false, textVersion: 'taslak', text: 'Aydınlatma metni.' })],
      'POST /api/v1/guardian/consent': [json(200, { accepted: true, textVersion: 'taslak', text: 'Aydınlatma metni.' })],
      'GET /api/v1/institution/me': [json(200, { id: 'k1', name: 'Atatürk Koleji', campuses: [] })],
    };
    const requests: Request[] = [];
    const fetch = jest.fn(async (input: RequestInfo | URL) => {
      const request = input as Request;
      requests.push(request);
      const next = responses[`${request.method} ${new URL(request.url).pathname}`]?.shift();
      if (!next) throw new Error(`unexpected ${request.method} ${request.url}`);
      return next;
    });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: secureTokenStore(storage), fetch });
    const loginHint = {
      read: async () => {
        const raw = await hints.getItemAsync('hint');
        return raw ? JSON.parse(raw) : null;
      },
      write: async (h: object) => hints.setItemAsync('hint', JSON.stringify(h)),
    };

    await render(<App client={client} loginHint={loginHint} />);
    await fireEvent.press(await screen.findByRole('button', { name: 'Veli girişi' }));
    await fireEvent.changeText(await screen.findByLabelText('Telefon numarası'), '0532 123 45 67');
    await fireEvent.changeText(screen.getByLabelText('Kurum kodu'), 'ATATURK-IST');
    await fireEvent.press(screen.getByRole('button', { name: 'SMS kodu gönder' }));
    await fireEvent.changeText(await screen.findByLabelText('6 haneli kod'), '482913');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));
    await fireEvent(await screen.findByLabelText('Verilerimin işlenmesine rıza veriyorum'), 'valueChange', true);
    await fireEvent.press(screen.getByRole('button', { name: 'Devam et' }));

    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Şifremi değiştir' })).toBeNull();
    expect(JSON.parse(storage.items.get('okul.session')!)).toEqual({ accessToken: 'veli-access', refreshToken: 'veli-refresh' });
    await waitFor(() => expect(hints.items.get('hint')).toContain('ATATURK-IST'));
    expect(hints.items.get('hint')).not.toContain('532');
    const code = requests.find((r) => r.url.endsWith('/auth/guardian/code'))!;
    expect(code.headers.get('Authorization')).toBeNull();
    const consent = requests.find((r) => r.url.endsWith('/guardian/consent'))!;
    expect(consent.headers.get('Authorization')).toBe('Bearer veli-access');
  });
});
