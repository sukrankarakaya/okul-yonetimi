import type { GuardianCodeResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { GuardianPhonePage } from './GuardianPhonePage';

/** Spec 0007, AC-1 / AC-3 / AC-9 — Veli Girişi (Telefon) on the web, every state with its literal text (B-7). */

async function fill(user: ReturnType<typeof userEvent.setup>, code: string, phone: string) {
  if (code) await user.type(screen.getByLabelText('Kurum kodu'), code);
  if (phone) await user.type(screen.getByLabelText('Telefon numarası'), phone);
}

describe('GuardianPhonePage', () => {
  it('sends with Enter after Tab order kurum kodu → telefon → button, a tel field', async () => {
    const user = userEvent.setup();
    const request = vi.fn(async (): Promise<GuardianCodeResult> => ({ kind: 'sent' }));
    const onSent = vi.fn();
    render(<GuardianPhonePage request={request} onSent={onSent} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Veli Girişi' })).toBeInTheDocument();
    await user.tab();
    expect(screen.getByLabelText('Kurum kodu')).toHaveFocus();
    await user.keyboard('ATATURK-IST');
    await user.tab();
    expect(screen.getByLabelText('Telefon numarası')).toHaveFocus();
    expect(screen.getByLabelText('Telefon numarası')).toHaveAttribute('type', 'tel');
    await user.keyboard('0532 123 45 67{Enter}');

    expect(request).toHaveBeenCalledWith('ATATURK-IST', '0532 123 45 67');
    expect(onSent).toHaveBeenCalledWith('ATATURK-IST', '0532 123 45 67');
  });

  it('marks empty fields "Bu alan gerekli." and sends nothing', async () => {
    const user = userEvent.setup();
    const request = vi.fn();
    render(<GuardianPhonePage request={request} onSent={() => undefined} />);

    await fill(user, 'ATATURK-IST', '');
    await user.click(screen.getByRole('button', { name: 'SMS kodu gönder' }));

    expect(screen.getByText('Bu alan gerekli.')).toBeInTheDocument();
    expect(screen.getByLabelText('Telefon numarası')).toHaveAttribute('aria-invalid', 'true');
    expect(request).not.toHaveBeenCalled();
  });

  it('shows "SMS kodu gönderiliyor..." and disables the button while sending', async () => {
    const user = userEvent.setup();
    let finish: (r: GuardianCodeResult) => void = () => undefined;
    const request = vi.fn(() => new Promise<GuardianCodeResult>((r) => (finish = r)));
    render(<GuardianPhonePage request={request} onSent={() => undefined} />);

    await fill(user, 'ATATURK-IST', '0532 123 45 67');
    await user.click(screen.getByRole('button', { name: 'SMS kodu gönder' }));

    expect(screen.getByRole('button')).toBeDisabled();
    expect(screen.getByRole('button')).toHaveTextContent('SMS kodu gönderiliyor...');
    await user.click(screen.getByRole('button'));
    expect(request).toHaveBeenCalledOnce();
    finish({ kind: 'sent' });
  });

  it.each([
    [{ kind: 'invalidPhone' } as const, 'Lütfen geçerli bir telefon numarası girin.'],
    [{ kind: 'rateLimited' } as const, 'Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.'],
    [{ kind: 'smsFailed' } as const, 'SMS gönderilemedi. Tekrar deneyin.'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('shows the text of %o', async (result, text) => {
    const user = userEvent.setup();
    render(<GuardianPhonePage request={async () => result} onSent={() => undefined} />);

    await fill(user, 'ATATURK-IST', '0532');
    await user.click(screen.getByRole('button', { name: 'SMS kodu gönder' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
  });

  it('keeps the fields given back after "Vazgeç" and shows a notice', () => {
    render(
      <GuardianPhonePage
        request={vi.fn()}
        onSent={() => undefined}
        initialCode="ATATURK-IST"
        initialPhone="0532 123 45 67"
        notice="Oturumunuzun süresi doldu, tekrar giriş yapın."
      />,
    );

    expect(screen.getByLabelText('Kurum kodu')).toHaveValue('ATATURK-IST');
    expect(screen.getByLabelText('Telefon numarası')).toHaveValue('0532 123 45 67');
    expect(screen.getByRole('status')).toHaveTextContent('Oturumunuzun süresi doldu, tekrar giriş yapın.');
  });
});
