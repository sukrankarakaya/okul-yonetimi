import type { LoginResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import { LoginScreen } from './LoginScreen';

/** Spec 0003, AC-1 / AC-10 — Giriş on mobile, every state with its literal text (B-7). */

async function fill(code: string, email: string, password: string) {
  await fireEvent.changeText(screen.getByLabelText('Kurum kodu'), code);
  await fireEvent.changeText(screen.getByLabelText('E-posta'), email);
  await fireEvent.changeText(screen.getByLabelText('Şifre'), password);
}

describe('LoginScreen', () => {
  let announce: jest.SpyInstance;
  beforeEach(() => {
    announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
  });
  afterEach(() => announce.mockRestore());

  it('logs in and reports the trimmed kurum kodu and e-posta to remember', async () => {
    const login = jest.fn(async (): Promise<LoginResult> => ({ kind: 'ok', roles: ['Teacher'] }));
    const onLoggedIn = jest.fn();
    await render(<LoginScreen login={login} onLoggedIn={onLoggedIn} />);

    await fill(' ATATURK-IST ', 'ogretmen@ataturk.test', 'dogru-sifre-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));

    await waitFor(() => expect(onLoggedIn).toHaveBeenCalledWith('ATATURK-IST', 'ogretmen@ataturk.test'));
    expect(login).toHaveBeenCalledWith(' ATATURK-IST ', 'ogretmen@ataturk.test', 'dogru-sifre-123');
  });

  it('starts with the remembered kurum kodu and e-posta — never a password', async () => {
    await render(<LoginScreen login={jest.fn()} onLoggedIn={jest.fn()} initialCode="ATATURK-IST" initialEmail="a@b.test" />);

    expect(screen.getByLabelText('Kurum kodu').props.value).toBe('ATATURK-IST');
    expect(screen.getByLabelText('E-posta').props.value).toBe('a@b.test');
    expect(screen.getByLabelText('Şifre').props.value).toBe('');
    expect(screen.getByLabelText('Şifre').props.secureTextEntry).toBe(true);
  });

  it('marks empty fields "Bu alan gerekli.", announces it, and sends nothing', async () => {
    const login = jest.fn();
    await render(<LoginScreen login={login} onLoggedIn={jest.fn()} />);

    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findAllByText('Bu alan gerekli.')).toHaveLength(3);
    await waitFor(() => expect(announce).toHaveBeenCalledWith('Bu alan gerekli.'));
    expect(login).not.toHaveBeenCalled();
  });

  it('is busy and disabled while sending — no double submit', async () => {
    let finish: (r: LoginResult) => void = () => undefined;
    const login = jest.fn(() => new Promise<LoginResult>((r) => (finish = r)));
    await render(<LoginScreen login={login} onLoggedIn={jest.fn()} />);

    await fill('ATATURK-IST', 'a@b.test', 'dogru-sifre-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Giriş yap' })).toBeDisabled());
    expect(screen.getByLabelText('Yükleniyor…')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(login).toHaveBeenCalledTimes(1);
    finish({ kind: 'invalid' });
    expect(await screen.findByText('Kurum kodu, e-posta veya şifre hatalı.')).toBeOnTheScreen();
  });

  it.each([
    [{ kind: 'invalid' } as LoginResult, 'Kurum kodu, e-posta veya şifre hatalı.'],
    [{ kind: 'locked' } as LoginResult, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
    [{ kind: 'network' } as LoginResult, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows and announces %o', async (result, text) => {
    await render(<LoginScreen login={async () => result} onLoggedIn={jest.fn()} />);

    await fill('ATATURK-IST', 'a@b.test', 'yanlis-sifre');
    await fireEvent.press(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByText(text)).toBeOnTheScreen();
    await waitFor(() => expect(announce).toHaveBeenCalledWith(text));
    if (result.kind === 'invalid') {
      expect(screen.getByLabelText('Şifre').props.value).toBe('');
      expect(screen.getByLabelText('Kurum kodu').props.value).toBe('ATATURK-IST');
    }
  });

  it('shows the "session expired" notice', async () => {
    await render(<LoginScreen login={jest.fn()} onLoggedIn={jest.fn()} notice="Oturumunuzun süresi doldu, tekrar giriş yapın." />);

    expect(screen.getByText('Oturumunuzun süresi doldu, tekrar giriş yapın.')).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Giriş' })).toBeOnTheScreen();
  });
});
