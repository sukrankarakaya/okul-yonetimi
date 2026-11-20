import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SetupStep, type SetupConfirm, type SetupLoad } from './SetupStep';

/** Spec 0004, AC-2 / AC-13 — Kurulum on the web, literal texts (B-7). */
const ready: SetupLoad = { kind: 'ok', key: 'JBSWY3DPEHPK3PXP', qrCode: 'data:image/png;base64,iVBORw0KGgo=' };

function renderStep(load: SetupLoad, confirm: SetupConfirm) {
  const props = {
    load: vi.fn(async () => load),
    confirm: vi.fn(async () => confirm),
    onConfirmed: vi.fn(),
    onEnded: vi.fn(),
    onCancel: vi.fn(),
  };
  render(<SetupStep {...props} />);
  return props;
}

describe('SetupStep', () => {
  it('shows the QR code, the key as text and confirms with the first code', async () => {
    const user = userEvent.setup();
    const props = renderStep(ready, { kind: 'ok', recoveryCodes: ['K7Q2-9XMD'] });

    expect(await screen.findByText('Doğrulama uygulamanızla QR kodunu okutun veya anahtarı elle girin.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'İki adımlı doğrulamayı kurun' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Doğrulama uygulaması için QR kodu' })).toHaveAttribute('src', ready.qrCode);
    expect(screen.getByText('JBSWY3DPEHPK3PXP')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Kopyala' })).toBeInTheDocument();
    await user.type(screen.getByLabelText('Doğrulama kodu'), '123456');
    await user.click(screen.getByRole('button', { name: 'Kurulumu tamamla' }));

    expect(props.confirm).toHaveBeenCalledWith('123456');
    expect(props.onConfirmed).toHaveBeenCalledWith(['K7Q2-9XMD']);
  });

  it.each([
    [{ kind: 'invalidCode' } as const, 'Kod hatalı veya süresi geçmiş.'],
    [{ kind: 'locked' } as const, 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.'],
    [{ kind: 'network' } as const, 'Bağlantı kurulamadı, tekrar deneyin.'],
  ])('a refused code shows %o and does not confirm', async (result, text) => {
    const user = userEvent.setup();
    const props = renderStep(ready, result);

    await user.type(await screen.findByLabelText('Doğrulama kodu'), '000000{Enter}');

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
    expect(props.onConfirmed).not.toHaveBeenCalled();
  });

  it('an ended step (at load or at confirm) leaves the setup; "Vazgeç" cancels', async () => {
    const user = userEvent.setup();
    const ended = renderStep({ kind: 'stepEnded' }, { kind: 'stepEnded' });
    await vi.waitFor(() => expect(ended.onEnded).toHaveBeenCalledWith({ kind: 'stepEnded' }));
    document.body.innerHTML = '';

    const props = renderStep(ready, { kind: 'stepEnded' });
    await user.type(await screen.findByLabelText('Doğrulama kodu'), '123456{Enter}');
    expect(props.onEnded).toHaveBeenCalledWith({ kind: 'stepEnded' });
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(props.onCancel).toHaveBeenCalledOnce();
  });
});
