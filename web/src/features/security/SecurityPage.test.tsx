import type { TwoFactorChangeResult, TwoFactorStatusResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SecurityPage } from './SecurityPage';

/** Spec 0004, AC-8 / AC-9 / AC-12 / AC-13 — Güvenlik on the web, literal texts (B-7). */
type Status = { enabled: boolean; required: boolean; canDisable: boolean; remainingRecoveryCodes: number };

function renderPage(status: Status, change: TwoFactorChangeResult = { kind: 'ok' }) {
  const result: TwoFactorStatusResult = { kind: 'ok', status };
  const props = {
    loadStatus: vi.fn(async () => result),
    renewRecoveryCodes: vi.fn(async () => change),
    disableTwoFactor: vi.fn(async () => change),
    onRenewed: vi.fn(),
    onEnable: vi.fn(),
    onSessionEnded: vi.fn(),
    onBack: vi.fn(),
  };
  render(<SecurityPage {...props} />);
  return props;
}

describe('SecurityPage', () => {
  it('mandatory: on, "Rolünüz gereği zorunlu.", the remaining count, no way to turn it off', async () => {
    renderPage({ enabled: true, required: true, canDisable: false, remainingRecoveryCodes: 7 });

    expect(await screen.findByText('İki adımlı doğrulama: Açık')).toBeInTheDocument();
    expect(screen.getByText('Rolünüz gereği zorunlu.')).toBeInTheDocument();
    expect(screen.getByText('Kalan kurtarma kodu: 7')).toBeInTheDocument();
    expect(screen.queryByText('Kurtarma kodlarınız azaldı. Yenilemeniz önerilir.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'İki adımlı doğrulamayı kapat' })).not.toBeInTheDocument();
  });

  it.each([3, 0])('suggests a renewal when %i codes are left', async (left) => {
    renderPage({ enabled: true, required: true, canDisable: false, remainingRecoveryCodes: left });

    expect(await screen.findByText('Kurtarma kodlarınız azaldı. Yenilemeniz önerilir.')).toBeInTheDocument();
  });

  it('renews the codes with a code and hands the new ones on', async () => {
    const user = userEvent.setup();
    const props = renderPage({ enabled: true, required: true, canDisable: false, remainingRecoveryCodes: 2 }, { kind: 'ok', recoveryCodes: ['K7Q2-9XMD'] });

    await user.click(await screen.findByRole('button', { name: 'Kurtarma kodlarını yenile' }));
    await user.type(screen.getByLabelText('Doğrulama kodu'), '123456{Enter}');

    expect(props.renewRecoveryCodes).toHaveBeenCalledWith('123456');
    expect(props.onRenewed).toHaveBeenCalledWith(['K7Q2-9XMD']);
  });

  it.each([
    [{ kind: 'invalidCode' } as const, 'Kod hatalı veya süresi geçmiş.'],
    [{ kind: 'locked' } as const, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('a refused code shows %o', async (change, text) => {
    const user = userEvent.setup();
    const props = renderPage({ enabled: true, required: false, canDisable: true, remainingRecoveryCodes: 10 }, change);

    await user.click(await screen.findByRole('button', { name: 'İki adımlı doğrulamayı kapat' }));
    await user.type(screen.getByLabelText('Doğrulama kodu'), '000000{Enter}');

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
    expect(props.onRenewed).not.toHaveBeenCalled();
  });

  it('Öğretmen: turns it off with a code ("İki adımlı doğrulama kapatıldı.") and can turn it on', async () => {
    const user = userEvent.setup();
    const props = renderPage({ enabled: true, required: false, canDisable: true, remainingRecoveryCodes: 10 });

    await user.click(await screen.findByRole('button', { name: 'İki adımlı doğrulamayı kapat' }));
    await user.type(screen.getByLabelText('Doğrulama kodu'), '123456{Enter}');

    expect(props.disableTwoFactor).toHaveBeenCalledWith('123456');
    expect(await screen.findByRole('status')).toHaveTextContent('İki adımlı doğrulama kapatıldı.');
    expect(props.loadStatus).toHaveBeenCalledTimes(2);
    document.body.innerHTML = '';

    const off = renderPage({ enabled: false, required: false, canDisable: false, remainingRecoveryCodes: 0 });
    expect(await screen.findByText('İki adımlı doğrulama: Kapalı')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'İki adımlı doğrulamayı aç' }));
    expect(off.onEnable).toHaveBeenCalledOnce();
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(off.onBack).toHaveBeenCalledOnce();
  });
});
