import type { ChangePasswordResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import { ChangePasswordScreen } from './ChangePasswordScreen';

/** Spec 0003, AC-7 / AC-10 — Şifre değiştir on mobile, literal texts (B-7). */

async function renderScreen(result: ChangePasswordResult) {
  const handlers = { onChanged: jest.fn(), onCancel: jest.fn(), onSessionEnded: jest.fn() };
  const changePassword = jest.fn(async () => result);
  await render(<ChangePasswordScreen changePassword={changePassword} {...handlers} />);
  await fireEvent.changeText(screen.getByLabelText('Mevcut şifre'), 'eski-sifre-123');
  await fireEvent.changeText(screen.getByLabelText('Yeni şifre'), 'yeni-sifre-1234');
  await fireEvent.changeText(screen.getByLabelText('Yeni şifre (tekrar)'), 'yeni-sifre-1234');
  await fireEvent.press(screen.getByRole('button', { name: 'Şifreyi değiştir' }));
  return { changePassword, ...handlers };
}

describe('ChangePasswordScreen', () => {
  let announce: jest.SpyInstance;
  beforeEach(() => {
    announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
  });
  afterEach(() => announce.mockRestore());

  it.each([
    ['tooShort' as const, 'Yeni şifre en az 10 karakter olmalı.'],
    ['mismatch' as const, 'Yeni şifreler eşleşmiyor.'],
    ['currentWrong' as const, 'Mevcut şifre hatalı.'],
    ['network' as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows and announces %s', async (result, text) => {
    await renderScreen(result);

    expect(await screen.findByText(text)).toBeOnTheScreen();
    await waitFor(() => expect(announce).toHaveBeenCalledWith(text));
  });

  it('sends the three fields and reports success', async () => {
    const { changePassword, onChanged } = await renderScreen('changed');

    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    expect(changePassword).toHaveBeenCalledWith('eski-sifre-123', 'yeni-sifre-1234', 'yeni-sifre-1234');
    expect(screen.getByLabelText('Yeni şifre').props.secureTextEntry).toBe(true);
  });

  it('goes back to Giriş when the session has ended, and "Vazgeç" cancels', async () => {
    const { onSessionEnded, onCancel } = await renderScreen('unauthenticated');

    await waitFor(() => expect(onSessionEnded).toHaveBeenCalledTimes(1));
    await fireEvent.press(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
