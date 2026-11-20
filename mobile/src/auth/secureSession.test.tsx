import { createOkulClient } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import App from '../../App';
import { secureTokenStore, type SecureStorage } from './secureSession';

/**
 * Spec 0003, AC-8 / AC-9 — on mobile the session lives in the device's secure store, nothing else; without a session
 * the app opens on Giriş with the remembered kurum kodu and e-posta (no automatic test login).
 */

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

const session = {
  roles: ['Teacher'],
  accessExpiresAt: '2026-11-19T10:15:00Z',
  sessionExpiresAt: '2026-12-19T10:00:00Z',
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
};

describe('secure session (mobile)', () => {
  it('opens on Giriş without a session, then keeps the tokens in the secure store and sends them as bearer', async () => {
    const storage = memoryStorage();
    const hints = memoryStorage();
    const requests: Request[] = [];
    const responses = [json(200, session), json(200, { id: 'k1', name: 'Atatürk Koleji', campuses: [] })];
    const fetch = jest.fn(async (input: RequestInfo | URL) => {
      requests.push(input as Request);
      return responses.shift()!;
    });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: secureTokenStore(storage), fetch });
    const loginHint = {
      read: async () => {
        const raw = await hints.getItemAsync('hint');
        return raw ? JSON.parse(raw) : { institutionCode: 'ATATURK-IST', email: 'ogretmen@ataturk.test' };
      },
      write: async (h: object) => hints.setItemAsync('hint', JSON.stringify(h)),
    };

    await render(<App client={client} loginHint={loginHint} />);

    expect(await screen.findByRole('header', { name: 'Giriş' })).toBeOnTheScreen();
    expect(fetch).not.toHaveBeenCalled(); // no stored session → no request, no test login
    expect(screen.getByLabelText('Kurum kodu').props.value).toBe('ATATURK-IST');
    await fireEvent.changeText(screen.getByLabelText('Şifre'), 'dogru-sifre-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
    expect(JSON.parse(storage.items.get('okul.session')!)).toEqual({ accessToken: 'access-1', refreshToken: 'refresh-1' });
    await waitFor(() => expect(hints.items.get('hint')).toContain('ATATURK-IST'));
    expect(hints.items.get('hint')).not.toContain('dogru-sifre-123');
    expect(requests[1]!.headers.get('Authorization')).toBe('Bearer access-1');
    expect(requests[0]!.headers.get('Authorization')).toBeNull();
  });

  it.each([
    [401, 'invalid'],
    [429, 'locked'],
    [500, 'network'],
    [403, 'network'],
  ])('login maps HTTP %i to %s (review round 1 #7)', async (status, kind) => {
    const fetch = jest.fn(async () => json(status, { detail: 'x' }));
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: secureTokenStore(memoryStorage()), fetch });

    expect((await client.login('ATATURK-IST', 'a@b.test', 'dogru-sifre-123')).kind).toBe(kind);
  });

  it.each([
    [400, 'password_mismatch', 'mismatch'],
    [400, 'current_password_wrong', 'currentWrong'],
    [500, undefined, 'network'],
    [403, undefined, 'network'],
    [429, undefined, 'locked'], // spec 0004 (B-11): wrong current passwords lock like the login (A-2)
  ])('change password maps HTTP %i (code %s) to %s (VERIFY round 1 F-2)', async (status, code, result) => {
    const storage = memoryStorage();
    await storage.setItemAsync('okul.session', JSON.stringify({ accessToken: 'access-1', refreshToken: 'refresh-1' }));
    const fetch = jest.fn(async () => json(status, { detail: 'x', code }));
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: secureTokenStore(storage), fetch });

    expect(await client.changePassword('eski-sifre-123', 'yeni-sifre-1234', 'yeni-sifre-1234')).toBe(result);
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('a completed 2FA step keeps the tokens in the secure store, never the step itself (spec 0004)', async () => {
    const storage = memoryStorage();
    const fetch = jest.fn(async (input: RequestInfo | URL) => {
      const url = (input as Request).url;
      return url.endsWith('/auth/login') ? json(202, { next: 'verify', step: 't.s.secret', stepExpiresAt: '2026-11-20T10:05:00Z' }) : json(200, session);
    });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: secureTokenStore(storage), fetch });

    expect(await client.login('ATATURK-IST', 'a@b.test', 'dogru-sifre-123')).toEqual({ kind: 'verify', step: 't.s.secret' });
    expect(storage.items.size).toBe(0);
    expect((await client.verifyLogin('t.s.secret', '123456')).kind).toBe('ok');
    expect(JSON.parse(storage.items.get('okul.session')!)).toEqual({ accessToken: 'access-1', refreshToken: 'refresh-1' });
  });

  it('app: right password → Doğrulama → code → Kurum bilgisi with Güvenlik; kurum kodu and e-posta remembered (spec 0004)', async () => {
    const storage = memoryStorage();
    const hints = memoryStorage();
    const responses = [
      json(202, { next: 'verify', step: 't.s.secret', stepExpiresAt: '2026-11-20T10:05:00Z' }),
      json(200, session),
      json(200, { id: 'k1', name: 'Atatürk Koleji', campuses: [] }),
    ];
    const fetch = jest.fn(async () => responses.shift()!);
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: secureTokenStore(storage), fetch });
    const loginHint = {
      read: async () => ({ institutionCode: 'ATATURK-IST', email: 'yonetici@ataturk.test' }),
      write: async (h: object) => hints.setItemAsync('hint', JSON.stringify(h)),
    };
    jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);

    await render(<App client={client} loginHint={loginHint} />);
    await fireEvent.changeText(await screen.findByLabelText('Şifre'), 'dogru-sifre-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));
    expect(await screen.findByRole('header', { name: 'İki adımlı doğrulama' })).toBeOnTheScreen();
    expect(storage.items.size).toBe(0); // the step is not a session
    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '123456');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Güvenlik' })).toBeOnTheScreen();
    expect(JSON.parse(storage.items.get('okul.session')!)).toEqual({ accessToken: 'access-1', refreshToken: 'refresh-1' });
    expect(hints.items.get('hint')).toContain('ATATURK-IST');
  });

  it('logout clears the secure store', async () => {
    const storage = memoryStorage();
    const store = secureTokenStore(storage);
    await store.write({ accessToken: 'a', refreshToken: 'r' });
    const fetch = jest.fn(async () => new Response(null, { status: 204 }));
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'mobile', tokenStore: store, fetch });

    await client.logout();

    expect(storage.items.size).toBe(0);
  });
});
