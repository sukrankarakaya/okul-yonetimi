import type { GuardianCodeResult, GuardianVerifyResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import { GuardianCodeScreen } from './GuardianCodeScreen';

/** Spec 0007, AC-2 / AC-3 / AC-9 — SMS kodu on mobile (G-1 Vazgeç, G-2 tekrar gönder). */

async function screenWith(overrides: Partial<Parameters<typeof GuardianCodeScreen>[0]> = {}) {
  const props = {
    verify: jest.fn(async (): Promise<GuardianVerifyResult> => ({ kind: 'ok', consentRequired: false })),
    resend: jest.fn(async (): Promise<GuardianCodeResult> => ({ kind: 'sent' })),
    onCancel: jest.fn(),
    onLoggedIn: jest.fn(),
    ...overrides,
  };
  await render(<GuardianCodeScreen {...props} />);
  return props;
}

describe('GuardianCodeScreen', () => {
  let announce: jest.SpyInstance;
  beforeEach(() => {
    announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
  });
  afterEach(() => announce.mockRestore());

  it('announces the same "sent" text for every number; a number pad with OTP autofill', async () => {
    await screenWith();

    expect(screen.getByText('Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.')).toBeOnTheScreen();
    expect(announce).toHaveBeenCalledWith('Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.');
    const field = screen.getByLabelText('6 haneli kod');
    expect(field.props.keyboardType).toBe('number-pad');
    expect(field.props.textContentType).toBe('oneTimeCode');
    expect(field.props.autoComplete).toBe('one-time-code');
  });

  it('verifies and reports whether KVKK comes first', async () => {
    const props = await screenWith();

    await fireEvent.changeText(screen.getByLabelText('6 haneli kod'), '482913');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    await waitFor(() => expect(props.onLoggedIn).toHaveBeenCalledWith(false));
    expect(props.verify).toHaveBeenCalledWith('482913');
  });

  it.each([
    [{ kind: 'invalidCode' } as const, 'Kod hatalı veya süresi geçmiş.'],
    [{ kind: 'expired' } as const, 'Kodun süresi doldu. Yeni kod isteyin.'],
    [{ kind: 'locked' } as const, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
  ])('shows and announces the text of %o, clearing the code', async (result, text) => {
    await screenWith({ verify: async () => result });

    await fireEvent.changeText(screen.getByLabelText('6 haneli kod'), '000000');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    expect(await screen.findByText(text)).toBeOnTheScreen();
    expect(announce).toHaveBeenCalledWith(text);
    expect(screen.getByLabelText('6 haneli kod').props.value).toBe('');
  });

  it('"SMS kodunu tekrar gönder" at the limit is disabled with the rate-limit text; "Vazgeç" goes back', async () => {
    const props = await screenWith({ resend: jest.fn(async (): Promise<GuardianCodeResult> => ({ kind: 'rateLimited' })) });

    await fireEvent.press(screen.getByRole('button', { name: 'SMS kodunu tekrar gönder' }));

    expect(await screen.findByText('Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'SMS kodunu tekrar gönder' })).toBeDisabled();
    await fireEvent.press(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(props.onCancel).toHaveBeenCalledTimes(1);
  });
});
