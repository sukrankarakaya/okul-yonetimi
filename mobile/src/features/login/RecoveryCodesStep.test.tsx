import { fireEvent, render, screen } from '@testing-library/react-native';
import * as Clipboard from 'expo-clipboard';
import { AccessibilityInfo } from 'react-native';
import { RecoveryCodesStep } from './RecoveryCodesStep';

jest.mock('expo-clipboard', () => ({ setStringAsync: jest.fn(async () => true) }));

/** Spec 0004, AC-3 / AC-13 — Kurtarma kodları on mobile, literal texts (B-7). */
describe('RecoveryCodesStep (mobile)', () => {
  it('lists the codes with the announced warning, copies them, continues on "Kaydettim, devam et"', async () => {
    const announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
    const onContinue = jest.fn();
    await render(<RecoveryCodesStep codes={['K7Q2-9XMD', 'P4TR-2HWA']} notice="Yeni kurtarma kodlarınız oluşturuldu." onContinue={onContinue} />);

    expect(screen.getByRole('header', { name: 'Kurtarma kodlarınız' })).toBeOnTheScreen();
    expect(screen.getByText('Bu kodlar bir daha gösterilmeyecek. Güvenli bir yere kaydedin.')).toBeOnTheScreen();
    expect(announce).toHaveBeenCalledWith('Bu kodlar bir daha gösterilmeyecek. Güvenli bir yere kaydedin.');
    expect(screen.getByText('Yeni kurtarma kodlarınız oluşturuldu.')).toBeOnTheScreen();
    expect(screen.getByText('K7Q2-9XMD')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Kopyala' }));
    expect(Clipboard.setStringAsync).toHaveBeenCalledWith('K7Q2-9XMD\nP4TR-2HWA');
    expect(onContinue).not.toHaveBeenCalled();
    await fireEvent.press(screen.getByRole('button', { name: 'Kaydettim, devam et' }));
    expect(onContinue).toHaveBeenCalledTimes(1);
    announce.mockRestore();
  });
});
