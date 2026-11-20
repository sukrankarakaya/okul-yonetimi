import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RecoveryCodesStep } from './RecoveryCodesStep';

/** Spec 0004, AC-3 / AC-13 — Kurtarma kodları, shown once (literal texts, B-7). */
const codes = ['K7Q2-9XMD', 'P4TR-2HWA', 'B8NE-6LCU', 'M3VY-7QKD', 'H9ZS-4RPE', 'W2CX-8JNF', 'T6GA-3MBQ', 'R5UD-9YKH', 'E8LP-2SWT', 'N4FJ-7VZC'];

describe('RecoveryCodesStep', () => {
  it('lists the ten codes with the warning, copy and download, and continues only on "Kaydettim, devam et"', async () => {
    const user = userEvent.setup();
    const onContinue = vi.fn();
    render(<RecoveryCodesStep codes={codes} onContinue={onContinue} />);

    expect(screen.getByRole('heading', { name: 'Kurtarma kodlarınız' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Bu kodlar bir daha gösterilmeyecek. Güvenli bir yere kaydedin.');
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual(codes);
    expect(screen.getByRole('button', { name: 'Kopyala' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'İndir' })).toHaveAttribute('download', 'okul-kurtarma-kodlari.txt');
    expect(onContinue).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Kaydettim, devam et' }));
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it('shows a notice after a renewal', () => {
    render(<RecoveryCodesStep codes={codes} notice="Yeni kurtarma kodlarınız oluşturuldu." onContinue={() => undefined} />);

    expect(screen.getByRole('status')).toHaveTextContent('Yeni kurtarma kodlarınız oluşturuldu.');
  });
});
