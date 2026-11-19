import { createOkulClient, type InstitutionResult } from '@okul/api-client';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { InstitutionPage, texts } from './InstitutionPage';

/** Spec 0001, AC-8 — web states, plain-text names, accessibility. */

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

/** The real api-client over a scripted fetch: proves the HTTP → page-state mapping too. */
function clientReturning(...responses: (() => Response)[]) {
  const fetch = vi.fn(async () => {
    const next = responses.shift();
    if (!next) throw new Error('unexpected request');
    return next();
  });
  const client = createOkulClient({ baseUrl: 'http://api.test', getToken: () => 'token', fetch });
  return { load: () => client.getMyInstitution(), fetch };
}

describe('InstitutionPage', () => {
  it('shows the loading state until the data arrives', async () => {
    let resolve: (r: InstitutionResult) => void = () => undefined;
    render(<InstitutionPage loadInstitution={() => new Promise((r) => (resolve = r))} />);

    expect(screen.getByRole('status')).toHaveTextContent(texts.loading);

    resolve({ kind: 'ok', institution });
    expect(await screen.findByRole('heading', { level: 1, name: 'Atatürk Koleji' })).toBeInTheDocument();
  });

  it('shows the kurum name as heading and page title, and the campuses as a labelled list in order', async () => {
    const { load } = clientReturning(() => json(200, institution));
    render(<InstitutionPage loadInstitution={load} />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Atatürk Koleji' })).toBeInTheDocument();
    expect(document.title).toBe('Atatürk Koleji');
    const list = screen.getByRole('list', { name: texts.campusList });
    expect(within(list).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['Cevizli', 'Çankaya', 'Ilgaz']);
  });

  it('shows the empty state when the kurum has no campuses', async () => {
    const { load } = clientReturning(() => json(200, { ...institution, campuses: [] }));
    render(<InstitutionPage loadInstitution={load} />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Atatürk Koleji' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent(texts.empty);
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('shows the error state and "Tekrar dene" requests again — also by keyboard (Tab + Enter)', async () => {
    const user = userEvent.setup();
    const { load, fetch } = clientReturning(() => json(500), () => json(200, institution));
    render(<InstitutionPage loadInstitution={load} />);

    expect(await screen.findByRole('alert')).toHaveTextContent(texts.error);
    await user.tab();
    expect(screen.getByRole('button', { name: texts.retry })).toHaveFocus();
    await user.keyboard('{Enter}');

    expect(await screen.findByRole('heading', { level: 1, name: 'Atatürk Koleji' })).toBeInTheDocument();
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('shows the error state when the network fails', async () => {
    const { load } = clientReturning(() => {
      throw new TypeError('Failed to fetch');
    });
    render(<InstitutionPage loadInstitution={load} />);

    expect(await screen.findByRole('alert')).toHaveTextContent(texts.error);
    expect(screen.getByRole('button', { name: texts.retry })).toBeInTheDocument();
  });

  it('shows "Oturum açmanız gerekiyor." without any kurum data when there is no (or an expired) session', async () => {
    const { load } = clientReturning(() => json(401));
    render(<InstitutionPage loadInstitution={load} />);

    expect(await screen.findByText(texts.unauthenticated)).toBeInTheDocument();
    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    expect(screen.queryByText('Atatürk Koleji')).not.toBeInTheDocument();
    expect(document.title).toBe(texts.defaultTitle);
  });

  it('renders names with special characters as plain text, never as markup', async () => {
    const tricky = { ...institution, name: 'Ali & Veli <b>Koleji</b>', campuses: [{ id: 'x', name: '<img src=x onerror=alert(1)>Şube' }] };
    const { load } = clientReturning(() => json(200, tricky));
    const { container } = render(<InstitutionPage loadInstitution={load} />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Ali & Veli <b>Koleji</b>' })).toBeInTheDocument();
    expect(screen.getByText('<img src=x onerror=alert(1)>Şube')).toBeInTheDocument();
    expect(container.querySelector('b, img')).toBeNull();
  });
});
