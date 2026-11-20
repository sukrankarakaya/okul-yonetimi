import type { GuardianCodeResult, GuardianVerifyResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { GuardianCodePage } from './GuardianCodePage';

/** Spec 0007, AC-2 / AC-3 / AC-9 — SMS kodu on the web (G-1 Vazgeç, G-2 tekrar gönder). */

function page(overrides: Partial<Parameters<typeof GuardianCodePage>[0]> = {}) {
  const props = {
    verify: vi.fn(async (): Promise<GuardianVerifyResult> => ({ kind: 'ok', consentRequired: true })),
    resend: vi.fn(async (): Promise<GuardianCodeResult> => ({ kind: 'sent' })),
    onCancel: vi.fn(),
    onLoggedIn: vi.fn(),
    ...overrides,
  };
  render(<GuardianCodePage {...props} />);
  return props;
}

describe('GuardianCodePage', () => {
  it('says the code was sent (same text for every number), with a one-time-code numeric field', () => {
    page();

    expect(screen.getByRole('status')).toHaveTextContent('Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.');
    expect(screen.getByRole('heading', { level: 1, name: 'SMS Kodunu Girin' })).toBeInTheDocument();
    const field = screen.getByLabelText('6 haneli kod');
    expect(field).toHaveAttribute('inputmode', 'numeric');
    expect(field).toHaveAttribute('autocomplete', 'one-time-code');
  });

  it('verifies with Enter and reports whether KVKK comes first', async () => {
    const user = userEvent.setup();
    const props = page();

    await user.type(screen.getByLabelText('6 haneli kod'), '482913{Enter}');

    expect(props.verify).toHaveBeenCalledWith('482913');
    expect(props.onLoggedIn).toHaveBeenCalledWith(true);
  });

  it('marks an empty code "Bu alan gerekli."', async () => {
    const user = userEvent.setup();
    const props = page();

    await user.click(screen.getByRole('button', { name: 'Doğrula' }));

    expect(screen.getByText('Bu alan gerekli.')).toBeInTheDocument();
    expect(props.verify).not.toHaveBeenCalled();
  });

  it('shows "Doğrulanıyor..." while verifying', async () => {
    const user = userEvent.setup();
    let finish: (r: GuardianVerifyResult) => void = () => undefined;
    page({ verify: vi.fn(() => new Promise<GuardianVerifyResult>((r) => (finish = r))) });

    await user.type(screen.getByLabelText('6 haneli kod'), '482913');
    await user.click(screen.getByRole('button', { name: 'Doğrula' }));

    expect(screen.getByRole('button', { name: 'Doğrulanıyor...' })).toBeDisabled();
    finish({ kind: 'invalidCode' });
  });

  it.each([
    [{ kind: 'invalidCode' } as const, 'Kod hatalı veya süresi geçmiş.'],
    [{ kind: 'expired' } as const, 'Kodun süresi doldu. Yeni kod isteyin.'],
    [{ kind: 'locked' } as const, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows the text of %o and clears the code', async (result, text) => {
    const user = userEvent.setup();
    page({ verify: async () => result });

    await user.type(screen.getByLabelText('6 haneli kod'), '000000');
    await user.click(screen.getByRole('button', { name: 'Doğrula' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
    expect(screen.getByLabelText('6 haneli kod')).toHaveValue('');
  });

  it('"SMS kodunu tekrar gönder" at the limit: disabled, with the rate-limit text', async () => {
    const user = userEvent.setup();
    const props = page({ resend: vi.fn(async (): Promise<GuardianCodeResult> => ({ kind: 'rateLimited' })) });

    await user.click(screen.getByRole('button', { name: 'SMS kodunu tekrar gönder' }));

    expect(props.resend).toHaveBeenCalledOnce();
    expect(await screen.findByRole('alert')).toHaveTextContent('Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.');
    expect(screen.getByRole('button', { name: 'SMS kodunu tekrar gönder' })).toBeDisabled();
  });

  it('"Vazgeç" goes back', async () => {
    const user = userEvent.setup();
    const props = page();

    await user.click(screen.getByRole('button', { name: 'Vazgeç' }));

    expect(props.onCancel).toHaveBeenCalledOnce();
  });
});
