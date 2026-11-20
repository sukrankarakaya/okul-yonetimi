import type { GuardianConsentResult } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ConsentPage } from './ConsentPage';

/** Spec 0007, AC-4 / AC-9 — KVKK on the web: "Devam et" only with the box ticked. */

const consent = (accepted: boolean): GuardianConsentResult => ({
  kind: 'ok',
  consent: { accepted, textVersion: 'taslak', text: 'Aydınlatma metni burada.' },
});

describe('ConsentPage', () => {
  it('shows the text; "Devam et" is disabled with the hint until the box is ticked, then records the rıza', async () => {
    const user = userEvent.setup();
    const accept = vi.fn(async () => consent(true));
    const onAccepted = vi.fn();
    render(<ConsentPage load={async () => consent(false)} accept={accept} onAccepted={onAccepted} onSessionEnded={vi.fn()} />);

    expect(screen.getByRole('heading', { level: 1, name: 'KVKK Aydınlatması' })).toBeInTheDocument();
    expect(await screen.findByText('Aydınlatma metni burada.')).toBeInTheDocument();
    const submit = screen.getByRole('button', { name: 'Devam et' });
    expect(submit).toBeDisabled();
    expect(screen.getByText('Lütfen rıza onayını işaretleyin.')).toBeInTheDocument();

    await user.click(screen.getByLabelText('Verilerimin işlenmesine rıza veriyorum'));
    expect(screen.queryByText('Lütfen rıza onayını işaretleyin.')).not.toBeInTheDocument();
    await user.click(submit);

    expect(accept).toHaveBeenCalledOnce();
    expect(onAccepted).toHaveBeenCalledOnce();
  });

  it('the text box is reachable by keyboard', async () => {
    const user = userEvent.setup();
    render(<ConsentPage load={async () => consent(false)} accept={vi.fn()} onAccepted={vi.fn()} onSessionEnded={vi.fn()} />);
    await screen.findByText('Aydınlatma metni burada.');

    await user.tab();
    expect(screen.getByLabelText('KVKK Aydınlatması')).toHaveFocus();
    await user.tab();
    expect(screen.getByLabelText('Verilerimin işlenmesine rıza veriyorum')).toHaveFocus();
  });

  it('an ended session goes back to login', async () => {
    const onSessionEnded = vi.fn();
    render(
      <ConsentPage load={async () => ({ kind: 'unauthenticated' })} accept={vi.fn()} onAccepted={vi.fn()} onSessionEnded={onSessionEnded} />,
    );

    await vi.waitFor(() => expect(onSessionEnded).toHaveBeenCalledOnce());
  });
});
