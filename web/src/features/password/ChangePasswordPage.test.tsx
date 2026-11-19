import type { ChangePasswordResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ChangePasswordPage } from './ChangePasswordPage';

/** Spec 0003, AC-7 / AC-10 — Şifre değiştir on the web, literal texts (B-7). */

function renderPage(result: ChangePasswordResult, handlers: Partial<Record<'onChanged' | 'onCancel' | 'onSessionEnded', () => void>> = {}) {
  const changePassword = vi.fn(async () => result);
  render(
    <ChangePasswordPage
      changePassword={changePassword}
      onChanged={handlers.onChanged ?? (() => undefined)}
      onCancel={handlers.onCancel ?? (() => undefined)}
      onSessionEnded={handlers.onSessionEnded ?? (() => undefined)}
    />,
  );
  return changePassword;
}

async function submit(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Mevcut şifre'), 'eski-sifre-123');
  await user.type(screen.getByLabelText('Yeni şifre'), 'yeni-sifre-1234');
  await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'yeni-sifre-1234');
  await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));
}

describe('ChangePasswordPage', () => {
  it.each([
    ['tooShort' as const, 'Yeni şifre en az 10 karakter olmalı.'],
    ['mismatch' as const, 'Yeni şifreler eşleşmiyor.'],
    ['currentWrong' as const, 'Mevcut şifre hatalı.'],
    ['network' as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows %s as an announced alert', async (result, text) => {
    const user = userEvent.setup();
    renderPage(result);

    await submit(user);

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
  });

  it('sends the three fields and reports success', async () => {
    const user = userEvent.setup();
    const onChanged = vi.fn();
    const changePassword = renderPage('changed', { onChanged });

    await submit(user);

    expect(changePassword).toHaveBeenCalledWith('eski-sifre-123', 'yeni-sifre-1234', 'yeni-sifre-1234');
    expect(onChanged).toHaveBeenCalledOnce();
  });

  it('goes back to Giriş when the session has ended, and "Vazgeç" cancels', async () => {
    const user = userEvent.setup();
    const onSessionEnded = vi.fn();
    const onCancel = vi.fn();
    renderPage('unauthenticated', { onSessionEnded, onCancel });

    await submit(user);
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }));

    expect(onSessionEnded).toHaveBeenCalledOnce();
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('uses new-password fields for the password manager', () => {
    renderPage('changed');

    expect(screen.getByLabelText('Yeni şifre')).toHaveAttribute('autocomplete', 'new-password');
    expect(screen.getByLabelText('Mevcut şifre')).toHaveAttribute('type', 'password');
  });
});
