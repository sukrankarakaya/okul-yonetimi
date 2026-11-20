import type { VerifyResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import { VerifyStep } from './VerifyStep';

/** Spec 0004, AC-4 / AC-6 / AC-13 — Doğrulama on mobile, literal texts (B-7). */
async function renderStep(answer: () => Promise<VerifyResult>) {
  const props = { verify: jest.fn(answer), verifyWithRecoveryCode: jest.fn(answer), onVerified: jest.fn(), onStepEnded: jest.fn(), onCancel: jest.fn() };
  await render(<VerifyStep {...props} />);
  return props;
}

describe('VerifyStep (mobile)', () => {
  let announce: jest.SpyInstance;
  beforeEach(() => {
    announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
  });
  afterEach(() => announce.mockRestore());

  it('a number pad, one-time-code field; the code verifies', async () => {
    const props = await renderStep(async () => ({ kind: 'ok', roles: [] }));

    expect(screen.getByRole('header', { name: 'İki adımlı doğrulama' })).toBeOnTheScreen();
    expect(screen.getByText('Doğrulama uygulamanızdaki 6 haneli kodu girin.')).toBeOnTheScreen();
    const field = screen.getByLabelText('Doğrulama kodu');
    expect(field.props.keyboardType).toBe('number-pad');
    expect(field.props.textContentType).toBe('oneTimeCode');
    await fireEvent.changeText(field, '123456');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    await waitFor(() => expect(props.onVerified).toHaveBeenCalledTimes(1));
    expect(props.verify).toHaveBeenCalledWith('123456');
  });

  it('empty: "Bu alan gerekli." and nothing is sent', async () => {
    const props = await renderStep(async () => ({ kind: 'ok', roles: [] }));

    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    expect(await screen.findByText('Bu alan gerekli.')).toBeOnTheScreen();
    expect(props.verify).not.toHaveBeenCalled();
  });

  it.each([
    [{ kind: 'invalidCode' } as const, 'Kod hatalı veya süresi geçmiş.'],
    [{ kind: 'locked' } as const, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows and announces %o', async (result, text) => {
    await renderStep(async () => result);

    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '654321');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    expect(await screen.findByText(text)).toBeOnTheScreen();
    await waitFor(() => expect(announce).toHaveBeenCalledWith(text));
  });

  it('is busy and disabled while sending — no double submit', async () => {
    let finish: (r: VerifyResult) => void = () => undefined;
    const props = await renderStep(() => new Promise<VerifyResult>((r) => (finish = r)));

    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '123456');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Doğrula' })).toBeDisabled());
    expect(screen.getByRole('button', { name: 'Doğrula' })).toBeBusy();
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));

    expect(props.verify).toHaveBeenCalledTimes(1);
    finish({ kind: 'stepEnded' });
    await waitFor(() => expect(props.onStepEnded).toHaveBeenCalledTimes(1));
  });

  it('switches to a kurtarma kodu and back; "Vazgeç" cancels', async () => {
    const props = await renderStep(async () => ({ kind: 'ok', roles: [] }));

    await fireEvent.press(screen.getByRole('button', { name: 'Kurtarma kodu kullan' }));
    await fireEvent.changeText(screen.getByLabelText('Kurtarma kodu'), 'K7Q2-9XMD');
    await fireEvent.press(screen.getByRole('button', { name: 'Doğrula' }));
    await waitFor(() => expect(props.verifyWithRecoveryCode).toHaveBeenCalledWith('K7Q2-9XMD'));
    await fireEvent.press(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(props.onCancel).toHaveBeenCalledTimes(1);
  });
});
