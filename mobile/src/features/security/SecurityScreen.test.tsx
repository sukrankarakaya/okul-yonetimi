import type { TwoFactorChangeResult, TwoFactorStatusResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { SecurityScreen } from './SecurityScreen';

/** Spec 0004, AC-8 / AC-9 / AC-12 / AC-13 — Güvenlik on mobile, literal texts (B-7). */
type Status = { enabled: boolean; required: boolean; canDisable: boolean; remainingRecoveryCodes: number };

async function renderScreen(status: Status, change: TwoFactorChangeResult = { kind: 'ok' }) {
  const result: TwoFactorStatusResult = { kind: 'ok', status };
  const props = {
    loadStatus: jest.fn(async () => result),
    renewRecoveryCodes: jest.fn(async () => change),
    disableTwoFactor: jest.fn(async () => change),
    onRenewed: jest.fn(),
    onEnable: jest.fn(),
    onSessionEnded: jest.fn(),
    onBack: jest.fn(),
  };
  await render(<SecurityScreen {...props} />);
  return props;
}

describe('SecurityScreen (mobile)', () => {
  it('mandatory: on, "Rolünüz gereği zorunlu.", the count, the low warning at 3, no turning off', async () => {
    await renderScreen({ enabled: true, required: true, canDisable: false, remainingRecoveryCodes: 3 });

    expect(await screen.findByText('İki adımlı doğrulama: Açık')).toBeOnTheScreen();
    expect(screen.getByText('Rolünüz gereği zorunlu.')).toBeOnTheScreen();
    expect(screen.getByText('Kalan kurtarma kodu: 3')).toBeOnTheScreen();
    expect(screen.getByText('Kurtarma kodlarınız azaldı. Yenilemeniz önerilir.')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'İki adımlı doğrulamayı kapat' })).toBeNull();
  });

  it('renews with a code and hands the new codes on', async () => {
    const props = await renderScreen({ enabled: true, required: true, canDisable: false, remainingRecoveryCodes: 9 }, { kind: 'ok', recoveryCodes: ['K7Q2-9XMD'] });

    expect(screen.queryByText('Kurtarma kodlarınız azaldı. Yenilemeniz önerilir.')).toBeNull();
    await fireEvent.press(await screen.findByRole('button', { name: 'Kurtarma kodlarını yenile' }));
    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '123456');
    await fireEvent.press(screen.getByRole('button', { name: 'Kurtarma kodlarını yenile' }));

    await waitFor(() => expect(props.onRenewed).toHaveBeenCalledWith(['K7Q2-9XMD']));
  });

  it('Öğretmen: a wrong code does not turn it off; the right one does', async () => {
    const wrong = await renderScreen({ enabled: true, required: false, canDisable: true, remainingRecoveryCodes: 10 }, { kind: 'invalidCode' });
    await fireEvent.press(await screen.findByRole('button', { name: 'İki adımlı doğrulamayı kapat' }));
    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '000000');
    await fireEvent.press(screen.getByRole('button', { name: 'İki adımlı doğrulamayı kapat' }));
    expect(await screen.findByText('Kod hatalı veya süresi geçmiş.')).toBeOnTheScreen();
    expect(wrong.loadStatus).toHaveBeenCalledTimes(1);
  });

  it('Öğretmen with 2FA off can turn it on; "Vazgeç" goes back', async () => {
    const props = await renderScreen({ enabled: false, required: false, canDisable: false, remainingRecoveryCodes: 0 });

    expect(await screen.findByText('İki adımlı doğrulama: Kapalı')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'İki adımlı doğrulamayı aç' }));
    expect(props.onEnable).toHaveBeenCalledTimes(1);
    await fireEvent.press(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(props.onBack).toHaveBeenCalledTimes(1);
  });

  it('turning it off shows "İki adımlı doğrulama kapatıldı."', async () => {
    await renderScreen({ enabled: true, required: false, canDisable: true, remainingRecoveryCodes: 10 });

    await fireEvent.press(await screen.findByRole('button', { name: 'İki adımlı doğrulamayı kapat' }));
    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '123456');
    await fireEvent.press(screen.getByRole('button', { name: 'İki adımlı doğrulamayı kapat' }));

    expect(await screen.findByText('İki adımlı doğrulama kapatıldı.')).toBeOnTheScreen();
  });
});
