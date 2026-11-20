import type { GuardianConsentResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { ConsentScreen } from './ConsentScreen';

/** Spec 0007, AC-4 / AC-9 — KVKK on mobile: "Devam et" only with the rıza ticked. */

const consent = (accepted: boolean): GuardianConsentResult => ({
  kind: 'ok',
  consent: { accepted, textVersion: 'taslak', text: 'Aydınlatma metni burada.' },
});

describe('ConsentScreen', () => {
  it('"Devam et" is disabled with the hint until the rıza is given, then records it', async () => {
    const accept = jest.fn(async () => consent(true));
    const onAccepted = jest.fn();
    await render(<ConsentScreen load={async () => consent(false)} accept={accept} onAccepted={onAccepted} onSessionEnded={jest.fn()} />);

    expect(screen.getByRole('header', { name: 'KVKK Aydınlatması' })).toBeOnTheScreen();
    expect(await screen.findByText('Aydınlatma metni burada.')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Devam et' })).toBeDisabled();
    expect(screen.getByText('Lütfen rıza onayını işaretleyin.')).toBeOnTheScreen();

    await fireEvent(screen.getByLabelText('Verilerimin işlenmesine rıza veriyorum'), 'valueChange', true);
    expect(screen.queryByText('Lütfen rıza onayını işaretleyin.')).toBeNull();
    await fireEvent.press(screen.getByRole('button', { name: 'Devam et' }));

    await waitFor(() => expect(onAccepted).toHaveBeenCalledTimes(1));
    expect(accept).toHaveBeenCalledTimes(1);
  });

  it('an ended session goes back to login', async () => {
    const onSessionEnded = jest.fn();
    await render(
      <ConsentScreen load={async () => ({ kind: 'unauthenticated' })} accept={jest.fn()} onAccepted={jest.fn()} onSessionEnded={onSessionEnded} />,
    );

    await waitFor(() => expect(onSessionEnded).toHaveBeenCalledTimes(1));
  });
});
