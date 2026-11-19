import { createOkulClient, type InstitutionResult } from '@okul/api-client';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import { InstitutionScreen, texts } from './InstitutionScreen';

/** Spec 0001, AC-9 — mobile states, offline, plain-text names, accessibility. */

const institution = {
  id: '019a8f00-0000-7000-8000-00000000000a',
  name: 'Atatürk Koleji',
  campuses: [
    { id: 'c1', name: 'Cevizli' },
    { id: 'c2', name: 'Çankaya' },
    { id: 'c3', name: 'Ilgaz' },
  ],
};

function json(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

/** The real api-client over a scripted fetch: proves the HTTP → screen-state mapping too. */
function clientReturning(...responses: (() => Response)[]) {
  const fetch = jest.fn(async () => {
    const next = responses.shift();
    if (!next) throw new Error('unexpected request');
    return next();
  });
  const client = createOkulClient({ baseUrl: 'http://api.test', getToken: () => 'token', fetch });
  return { load: () => client.getMyInstitution(), fetch };
}

describe('InstitutionScreen', () => {
  let announce: jest.SpyInstance;
  beforeEach(() => {
    announce = jest.spyOn(AccessibilityInfo, 'announceForAccessibility').mockImplementation(() => undefined);
  });
  afterEach(() => announce.mockRestore());

  it('shows the loading state until the data arrives', async () => {
    let resolve: (r: InstitutionResult) => void = () => undefined;
    await render(<InstitutionScreen loadInstitution={() => new Promise((r) => (resolve = r))} />);

    expect(screen.getByText(texts.loading)).toBeOnTheScreen();
    expect(screen.getByLabelText(texts.loading)).toBeOnTheScreen();
    expect(announce).toHaveBeenCalledWith(texts.loading);

    resolve({ kind: 'ok', institution });
    expect(await screen.findByRole('header', { name: institution.name })).toBeOnTheScreen();
  });

  it('shows the kurum name as header and the campuses as a list, in the given order', async () => {
    const { load } = clientReturning(() => json(200, institution));
    await render(<InstitutionScreen loadInstitution={load} />);

    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
    expect(screen.getByLabelText(texts.campusList)).toBeOnTheScreen();
    expect(screen.getAllByRole('listitem').map((item) => item.props.children)).toEqual(['Cevizli', 'Çankaya', 'Ilgaz']);
  });

  it('shows the empty state when the kurum has no campuses', async () => {
    const { load } = clientReturning(() => json(200, { ...institution, campuses: [] }));
    await render(<InstitutionScreen loadInstitution={load} />);

    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
    expect(screen.getByText(texts.empty)).toBeOnTheScreen();
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
    await waitFor(() => expect(announce).toHaveBeenCalledWith(`Atatürk Koleji. ${texts.empty}`));
  });

  it('shows the error state on a server error, and "Tekrar dene" requests again', async () => {
    const { load, fetch } = clientReturning(() => json(500), () => json(200, institution));
    await render(<InstitutionScreen loadInstitution={load} />);

    expect(await screen.findByText(texts.error)).toBeOnTheScreen();
    await waitFor(() => expect(announce).toHaveBeenCalledWith(texts.error));
    await fireEvent.press(screen.getByRole('button', { name: texts.retry }));

    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('shows the error state without a connection, and retry works once it is back', async () => {
    const { load } = clientReturning(
      () => {
        throw new TypeError('Network request failed');
      },
      () => json(200, institution),
    );
    await render(<InstitutionScreen loadInstitution={load} />);

    expect(await screen.findByText(texts.error)).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: texts.retry }));
    expect(await screen.findByRole('header', { name: 'Atatürk Koleji' })).toBeOnTheScreen();
  });

  it('shows "Oturum açmanız gerekiyor." without any kurum data when there is no (or an expired) session', async () => {
    const { load } = clientReturning(() => json(401));
    await render(<InstitutionScreen loadInstitution={load} />);

    expect(await screen.findByText(texts.unauthenticated)).toBeOnTheScreen();
    expect(screen.queryByText('Atatürk Koleji')).toBeNull();
    expect(screen.queryByRole('header')).toBeNull();
    await waitFor(() => expect(announce).toHaveBeenCalledWith(texts.unauthenticated));
  });

  it('renders names with special characters as plain text', async () => {
    const tricky = { ...institution, name: 'Ali & Veli <b>Koleji</b>', campuses: [{ id: 'x', name: '<script>Şube</script>' }] };
    const { load } = clientReturning(() => json(200, tricky));
    await render(<InstitutionScreen loadInstitution={load} />);

    expect(await screen.findByRole('header', { name: 'Ali & Veli <b>Koleji</b>' })).toBeOnTheScreen();
    expect(screen.getByText('<script>Şube</script>')).toBeOnTheScreen();
  });
});
