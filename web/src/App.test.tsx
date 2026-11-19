import { createOkulClient } from '@okul/api-client';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

/**
 * Spec 0003 — the web app flow over the real api-client and a scripted backend: opens on Giriş without a session
 * (AC-9), logs in and out (AC-1, AC-6), keeps nothing in browser storage (AC-8), renews silently with ONE refresh for
 * parallel requests (AC-5) and returns to Giriş with the expired notice when the session has ended.
 */

const institution = { id: 'k1', name: 'Atatürk Koleji', campuses: [{ id: 'c1', name: 'Cevizli' }] };
const session = { roles: ['Teacher'], accessExpiresAt: '2026-11-19T10:15:00Z', sessionExpiresAt: '2026-11-19T22:00:00Z' };

function json(status: number, body?: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

/** A tiny fake backend keyed by "METHOD path", with a call log. */
function backend(routes: Record<string, (() => Response)[]>) {
  const calls: string[] = [];
  const fetch = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request; // openapi-fetch always passes a Request
    const key = `${request.method} ${new URL(request.url).pathname}`;
    calls.push(key);
    expect(request.headers.get('X-Okul-Csrf')).toBe('1');
    expect(request.headers.get('Authorization')).toBeNull();
    const next = routes[key]?.shift();
    if (!next) throw new Error(`unexpected ${key}`);
    return next();
  });
  return { fetch, calls };
}

describe('App (web)', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('opens on Giriş without a session, logs in, shows the kurum, logs out — storing nothing in the browser', async () => {
    const user = userEvent.setup();
    const { fetch, calls } = backend({
      'GET /api/v1/auth/session': [() => json(401)],
      'POST /api/v1/auth/refresh': [() => json(401)],
      'POST /api/v1/auth/login': [() => json(200, session)],
      'GET /api/v1/institution/me': [() => json(200, institution)],
      'POST /api/v1/auth/logout': [() => new Response(null, { status: 204 })],
    });
    render(<App client={createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch })} />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Giriş' })).toBeInTheDocument();
    await user.type(screen.getByLabelText('Kurum kodu'), 'ATATURK-IST');
    await user.type(screen.getByLabelText('E-posta'), 'ogretmen@ataturk.test');
    await user.type(screen.getByLabelText('Şifre'), 'dogru-sifre-123');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Atatürk Koleji' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Çıkış yap' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Giriş' })).toBeInTheDocument();

    expect(calls).toContain('POST /api/v1/auth/logout');
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('returns to Giriş with "Oturumunuzun süresi doldu…" when the session can no longer be renewed', async () => {
    const { fetch } = backend({
      'GET /api/v1/auth/session': [() => json(200, session)],
      'GET /api/v1/institution/me': [() => json(401)],
      'POST /api/v1/auth/refresh': [() => json(401)],
    });
    render(<App client={createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch })} />);

    expect(await screen.findByText('Oturumunuzun süresi doldu, tekrar giriş yapın.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Giriş' })).toBeInTheDocument();
    expect(screen.queryByText('Atatürk Koleji')).not.toBeInTheDocument();
  });
});

describe('api-client login answers', () => {
  it.each([
    [401, 'invalid'],
    [429, 'locked'],
    [500, 'network'],
    [403, 'network'],
    [502, 'network'],
  ])('maps HTTP %i to %s', async (status, kind) => {
    const { fetch } = backend({ 'POST /api/v1/auth/login': [() => json(status, { detail: 'x' })] });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch });

    expect((await client.login('ATATURK-IST', 'a@b.test', 'dogru-sifre-123')).kind).toBe(kind);
  });

  it('shows "Bağlantı kurulamadı" — not "hatalı bilgi" — when the server fails, and keeps the password', async () => {
    const user = userEvent.setup();
    const { fetch } = backend({
      'GET /api/v1/auth/session': [() => json(401)],
      'POST /api/v1/auth/refresh': [() => json(401)],
      'POST /api/v1/auth/login': [() => json(500)],
    });
    render(<App client={createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch })} />);

    await user.type(await screen.findByLabelText('Kurum kodu'), 'ATATURK-IST');
    await user.type(screen.getByLabelText('E-posta'), 'a@b.test');
    await user.type(screen.getByLabelText('Şifre'), 'dogru-sifre-123');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Bağlantı kurulamadı, tekrar deneyin.');
    expect(screen.getByLabelText('Şifre')).toHaveValue('dogru-sifre-123');
  });
});

describe('api-client session renewal', () => {
  it('renews silently once for parallel requests that both found the access expired', async () => {
    const { fetch, calls } = backend({
      'GET /api/v1/institution/me': [() => json(401), () => json(401), () => json(200, institution), () => json(200, institution)],
      'POST /api/v1/auth/refresh': [() => json(200, session)],
    });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch });

    const [a, b] = await Promise.all([client.getMyInstitution(), client.getMyInstitution()]);

    expect(a.kind).toBe('ok');
    expect(b.kind).toBe('ok');
    expect(calls.filter((c) => c === 'POST /api/v1/auth/refresh')).toHaveLength(1);
  });

  it('treats a parallel-renewal conflict (409) as renewed and retries', async () => {
    const { fetch } = backend({
      'GET /api/v1/institution/me': [() => json(401), () => json(200, institution)],
      'POST /api/v1/auth/refresh': [() => json(409)],
    });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch });

    expect((await client.getMyInstitution()).kind).toBe('ok');
  });
});
