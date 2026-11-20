import type { VerifyResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { VerifyStep } from './VerifyStep';

/** Spec 0004, AC-4 / AC-6 / AC-13 — Doğrulama on the web, literal texts (B-7). */
function renderStep(result: VerifyResult | (() => Promise<VerifyResult>)) {
  const answer = typeof result === 'function' ? result : async () => result;
  const props = {
    verify: vi.fn(answer),
    verifyWithRecoveryCode: vi.fn(answer),
    onVerified: vi.fn(),
    onStepEnded: vi.fn(),
    onCancel: vi.fn(),
  };
  render(<VerifyStep {...props} />);
  return props;
}

describe('VerifyStep', () => {
  it('asks for the 6-digit code with a numeric, one-time-code field and verifies with Enter', async () => {
    const user = userEvent.setup();
    const props = renderStep({ kind: 'ok', roles: ['Administrator'] });

    expect(screen.getByRole('heading', { name: 'İki adımlı doğrulama' })).toBeInTheDocument();
    expect(screen.getByText('Doğrulama uygulamanızdaki 6 haneli kodu girin.')).toBeInTheDocument();
    const field = screen.getByLabelText('Doğrulama kodu');
    expect(field).toHaveAttribute('inputmode', 'numeric');
    expect(field).toHaveAttribute('autocomplete', 'one-time-code');
    await user.type(field, '123 456{Enter}');

    expect(props.verify).toHaveBeenCalledWith('123 456');
    expect(props.onVerified).toHaveBeenCalledOnce();
  });

  it('empty: "Bu alan gerekli." and nothing is sent', async () => {
    const user = userEvent.setup();
    const props = renderStep({ kind: 'ok', roles: [] });

    await user.click(screen.getByRole('button', { name: 'Doğrula' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Bu alan gerekli.');
    expect(props.verify).not.toHaveBeenCalled();
  });

  it.each([
    [{ kind: 'invalidCode' } as const, 'Kod hatalı veya süresi geçmiş.', ''],
    [{ kind: 'locked' } as const, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.', '654321'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.', '654321'],
  ])('shows %o as an announced alert', async (result, text, left) => {
    const user = userEvent.setup();
    renderStep(result);

    await user.type(screen.getByLabelText('Doğrulama kodu'), '654321{Enter}');

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
    expect(screen.getByLabelText('Doğrulama kodu')).toHaveValue(left); // a wrong code is cleared
  });

  it('is busy and disabled while sending — no double submit', async () => {
    const user = userEvent.setup();
    let finish: (r: VerifyResult) => void = () => undefined;
    const props = renderStep(() => new Promise<VerifyResult>((r) => (finish = r)));

    await user.type(screen.getByLabelText('Doğrulama kodu'), '123456{Enter}');

    const button = screen.getByRole('button', { name: 'Yükleniyor…' });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
    await user.click(button);
    expect(props.verify).toHaveBeenCalledOnce();
    finish({ kind: 'invalidCode' });
    expect(await screen.findByRole('alert')).toHaveTextContent('Kod hatalı veya süresi geçmiş.');
  });

  it('an ended step goes back to Giriş', async () => {
    const user = userEvent.setup();
    const props = renderStep({ kind: 'stepEnded' });

    await user.type(screen.getByLabelText('Doğrulama kodu'), '123456{Enter}');

    expect(props.onStepEnded).toHaveBeenCalledOnce();
  });

  it('switches to a kurtarma kodu and back; "Vazgeç" cancels', async () => {
    const user = userEvent.setup();
    const props = renderStep({ kind: 'ok', roles: [] });

    await user.click(screen.getByRole('button', { name: 'Kurtarma kodu kullan' }));
    expect(screen.getByText('Kaydettiğiniz kurtarma kodlarından birini girin. Her kod bir kez kullanılır.')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Kurtarma kodu'), 'k7q2-9xmd{Enter}');
    expect(props.verifyWithRecoveryCode).toHaveBeenCalledWith('k7q2-9xmd');
    expect(props.verify).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Doğrulama kodu kullan' }));
    expect(screen.getByLabelText('Doğrulama kodu')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(props.onCancel).toHaveBeenCalledOnce();
  });
});
