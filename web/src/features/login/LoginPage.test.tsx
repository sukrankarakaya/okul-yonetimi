import type { LoginResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LoginPage } from './LoginPage';

/** Spec 0003, AC-1 / AC-10 — Giriş on the web, every state with its literal text (B-7). */

async function fill(user: ReturnType<typeof userEvent.setup>, code: string, email: string, password: string) {
  if (code) await user.type(screen.getByLabelText('Kurum kodu'), code);
  if (email) await user.type(screen.getByLabelText('E-posta'), email);
  if (password) await user.type(screen.getByLabelText('Şifre'), password);
}

describe('LoginPage', () => {
  it('logs in with Enter after Tab order kurum kodu → e-posta → şifre → button', async () => {
    const user = userEvent.setup();
    const login = vi.fn(async (): Promise<LoginResult> => ({ kind: 'ok', roles: ['Teacher'] }));
    const onLoggedIn = vi.fn();
    render(<LoginPage login={login} onLoggedIn={onLoggedIn} />);

    await user.tab();
    expect(screen.getByLabelText('Kurum kodu')).toHaveFocus();
    await user.keyboard('ATATURK-IST');
    await user.tab();
    expect(screen.getByLabelText('E-posta')).toHaveFocus();
    await user.keyboard('ogretmen@ataturk.test');
    await user.tab();
    expect(screen.getByLabelText('Şifre')).toHaveFocus();
    await user.keyboard('dogru-sifre-123');
    await user.tab();
    expect(screen.getByRole('button', { name: 'Giriş yap' })).toHaveFocus();
    await user.click(screen.getByLabelText('Şifre'));
    await user.keyboard('{Enter}');

    expect(login).toHaveBeenCalledWith('ATATURK-IST', 'ogretmen@ataturk.test', 'dogru-sifre-123');
    expect(onLoggedIn).toHaveBeenCalledOnce();
  });

  it('marks empty fields "Bu alan gerekli." and sends nothing', async () => {
    const user = userEvent.setup();
    const login = vi.fn();
    render(<LoginPage login={login} onLoggedIn={() => undefined} />);

    await fill(user, 'ATATURK-IST', '', '');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(screen.getAllByText('Bu alan gerekli.')).toHaveLength(2);
    expect(screen.getByLabelText('E-posta')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Kurum kodu')).not.toHaveAttribute('aria-invalid');
    expect(login).not.toHaveBeenCalled();
  });

  it('disables the button while sending — no double submit', async () => {
    const user = userEvent.setup();
    let finish: (r: LoginResult) => void = () => undefined;
    const login = vi.fn(() => new Promise<LoginResult>((r) => (finish = r)));
    render(<LoginPage login={login} onLoggedIn={() => undefined} />);

    await fill(user, 'ATATURK-IST', 'a@b.test', 'dogru-sifre-123');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
    expect(button).toHaveTextContent('Yükleniyor…');
    await user.click(button);
    expect(login).toHaveBeenCalledOnce();
    finish({ kind: 'invalid' });
  });

  it('shows the one wrong-input answer, clears only the password', async () => {
    const user = userEvent.setup();
    render(<LoginPage login={async () => ({ kind: 'invalid' })} onLoggedIn={() => undefined} />);

    await fill(user, 'ATATURK-IST', 'a@b.test', 'yanlis-sifre');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Kurum kodu, e-posta veya şifre hatalı.');
    expect(screen.getByLabelText('Şifre')).toHaveValue('');
    expect(screen.getByLabelText('Kurum kodu')).toHaveValue('ATATURK-IST');
    expect(screen.getByLabelText('E-posta')).toHaveValue('a@b.test');
  });

  it.each([
    [{ kind: 'locked' } as LoginResult, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
    [{ kind: 'network' } as LoginResult, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows %o as an announced alert', async (result, text) => {
    const user = userEvent.setup();
    render(<LoginPage login={async () => result} onLoggedIn={() => undefined} />);

    await fill(user, 'ATATURK-IST', 'a@b.test', 'dogru-sifre-123');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
  });

  it('shows the "session expired" notice above the form', () => {
    render(
      <LoginPage login={vi.fn()} onLoggedIn={() => undefined} notice="Oturumunuzun süresi doldu, tekrar giriş yapın." />,
    );

    expect(screen.getByRole('status')).toHaveTextContent('Oturumunuzun süresi doldu, tekrar giriş yapın.');
    expect(screen.getByRole('heading', { level: 1, name: 'Giriş' })).toBeInTheDocument();
  });

  it('works with password managers: username and current-password fields', () => {
    render(<LoginPage login={vi.fn()} onLoggedIn={() => undefined} />);

    expect(screen.getByLabelText('E-posta')).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByLabelText('Şifre')).toHaveAttribute('autocomplete', 'current-password');
    expect(screen.getByLabelText('Şifre')).toHaveAttribute('type', 'password');
  });

  it('spec 0007: "Veli girişi" opens the veli login', async () => {
    const user = userEvent.setup();
    const onGuardian = vi.fn();
    render(<LoginPage login={vi.fn()} onLoggedIn={() => undefined} onGuardian={onGuardian} />);

    await user.click(screen.getByRole('button', { name: 'Veli girişi' }));

    expect(onGuardian).toHaveBeenCalledOnce();
  });
});
