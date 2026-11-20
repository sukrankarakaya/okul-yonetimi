import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as Clipboard from 'expo-clipboard';
import { SetupStep, type SetupConfirm, type SetupLoad } from './SetupStep';

jest.mock('expo-clipboard', () => ({ setStringAsync: jest.fn(async () => true) }));

/** Spec 0004, AC-2 / AC-13 — Kurulum on mobile, literal texts (B-7). */
const ready: SetupLoad = { kind: 'ok', key: 'JBSWY3DPEHPK3PXP', qrCode: 'data:image/png;base64,iVBORw0KGgo=' };

async function renderStep(load: SetupLoad, confirm: SetupConfirm) {
  const props = { load: jest.fn(async () => load), confirm: jest.fn(async () => confirm), onConfirmed: jest.fn(), onEnded: jest.fn(), onCancel: jest.fn() };
  await render(<SetupStep {...props} />);
  return props;
}

describe('SetupStep (mobile)', () => {
  it('shows the QR code and the key as text, copies the key, confirms with the first code', async () => {
    const props = await renderStep(ready, { kind: 'ok', recoveryCodes: ['K7Q2-9XMD'] });

    expect(await screen.findByText('Doğrulama uygulamanızla QR kodunu okutun veya anahtarı elle girin.')).toBeOnTheScreen();
    expect(screen.getByLabelText('Doğrulama uygulaması için QR kodu')).toBeOnTheScreen();
    expect(screen.getByText('JBSWY3DPEHPK3PXP')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Kopyala' }));
    expect(Clipboard.setStringAsync).toHaveBeenCalledWith('JBSWY3DPEHPK3PXP');
    await fireEvent.changeText(screen.getByLabelText('Doğrulama kodu'), '123456');
    await fireEvent.press(screen.getByRole('button', { name: 'Kurulumu tamamla' }));

    await waitFor(() => expect(props.onConfirmed).toHaveBeenCalledWith(['K7Q2-9XMD']));
  });

  it('a wrong code shows "Kod hatalı veya süresi geçmiş." and does not confirm', async () => {
    const props = await renderStep(ready, { kind: 'invalidCode' });

    await fireEvent.changeText(await screen.findByLabelText('Doğrulama kodu'), '000000');
    await fireEvent.press(screen.getByRole('button', { name: 'Kurulumu tamamla' }));

    expect(await screen.findByText('Kod hatalı veya süresi geçmiş.')).toBeOnTheScreen();
    expect(props.onConfirmed).not.toHaveBeenCalled();
  });

  it('an ended step leaves the setup', async () => {
    const props = await renderStep({ kind: 'stepEnded' }, { kind: 'stepEnded' });

    await waitFor(() => expect(props.onEnded).toHaveBeenCalledWith({ kind: 'stepEnded' }));
  });
});
