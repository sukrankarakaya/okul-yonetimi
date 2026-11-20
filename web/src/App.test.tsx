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

  it.each([
    [400, 'current_password_wrong', 'currentWrong'],
    [400, 'password_too_short', 'tooShort'],
    [400, 'password_mismatch', 'mismatch'],
    [400, undefined, 'network'],
    [500, undefined, 'network'],
    [403, undefined, 'network'],
    [429, undefined, 'locked'], // spec 0004 (B-11): wrong current passwords lock like the login (A-2)
  ])('change password maps HTTP %i (code %s) to %s (VERIFY round 1 F-2)', async (status, code, result) => {
    const { fetch } = backend({ 'POST /api/v1/auth/password': [() => json(status, { detail: 'x', code })] });
    const client = createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch });

    expect(await client.changePassword('eski-sifre-123', 'yeni-sifre-1234', 'yeni-sifre-1234')).toBe(result);
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

/** Spec 0004 — the api-client's 2FA answers: only the API's own codes are the user's; anything else is "Bağlantı kurulamadı". */
describe('api-client two-step login', () => {
  const step = 'tenant.step.secret';
  const client = (routes: Record<string, (() => Response)[]>) =>
    createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch: backend(routes).fetch });

  it.each([
    ['verify', { kind: 'verify', step }],
    ['setup', { kind: 'setup', step }],
  ])('login answers 202 %s with the step', async (next, expected) => {
    const c = client({ 'POST /api/v1/auth/login': [() => json(202, { next, step, stepExpiresAt: '2026-11-20T10:05:00Z' })] });

    expect(await c.login('ATATURK-IST', 'a@b.test', 'dogru-sifre-123')).toEqual(expected);
  });

  it.each([
    [200, session, 'ok'],
    [401, { code: 'invalid_code' }, 'invalidCode'],
    [401, { code: 'step_ended' }, 'stepEnded'],
    [429, { code: 'too_many_attempts' }, 'locked'],
    [500, undefined, 'network'],
    [401, undefined, 'network'],
  ])('verify maps HTTP %i (%o) to %s, by code and by recovery code', async (status, body, kind) => {
    const c = client({ 'POST /api/v1/auth/login/verify': [() => json(status, body), () => json(status, body)] });

    expect((await c.verifyLogin(step, '123456')).kind).toBe(kind);
    expect((await c.verifyWithRecoveryCode(step, 'K7Q2-9XMD')).kind).toBe(kind);
  });

  it('setup: starts with the key and QR, confirms with the recovery codes, or reports the ended step', async () => {
    const c = client({
      'POST /api/v1/auth/login/setup': [() => json(200, { key: 'JBSWY3DP', qrCode: 'data:image/png;base64,AA' }), () => json(401, { code: 'step_ended' })],
      'POST /api/v1/auth/login/setup/confirm': [() => json(200, { session, recoveryCodes: ['K7Q2-9XMD'] }), () => json(401, { code: 'invalid_code' })],
    });

    expect(await c.startSetup(step)).toEqual({ kind: 'ok', key: 'JBSWY3DP', qrCode: 'data:image/png;base64,AA' });
    expect(await c.startSetup(step)).toEqual({ kind: 'stepEnded' });
    expect(await c.confirmSetup(step, '123456')).toEqual({ kind: 'ok', roles: ['Teacher'], recoveryCodes: ['K7Q2-9XMD'] });
    expect((await c.confirmSetup(step, '000000')).kind).toBe('invalidCode');
  });

  it('cancel never fails, even when the server is unreachable', async () => {
    const c = createOkulClient({ baseUrl: 'http://api.test', kind: 'web', fetch: vi.fn(async () => { throw new TypeError('offline'); }) });

    await expect(c.cancelLogin(step)).resolves.toBeUndefined();
  });

  it.each([
    [204, undefined, { kind: 'ok' }],
    [200, { recoveryCodes: ['K7Q2-9XMD'] }, { kind: 'ok', recoveryCodes: ['K7Q2-9XMD'] }],
    [400, { code: 'invalid_code' }, { kind: 'invalidCode' }],
    [403, { code: 'two_factor_mandatory' }, { kind: 'notAllowed' }],
    [429, { code: 'too_many_attempts' }, { kind: 'locked' }],
    [502, undefined, { kind: 'network' }],
  ])('Güvenlik changes map HTTP %i to %o', async (status, body, expected) => {
    const c = client({ 'POST /api/v1/auth/two-factor/disable': [() => json(status, body)] });

    expect(await c.disableTwoFactor('123456')).toEqual(expected);
  });

  it('reads the Güvenlik status, and reports an ended session', async () => {
    const status = { enabled: true, required: true, canDisable: false, remainingRecoveryCodes: 3 };
    const c = client({
      'GET /api/v1/auth/two-factor': [() => json(200, status), () => json(401)],
      'POST /api/v1/auth/refresh': [() => json(401)],
    });

    expect(await c.getTwoFactor()).toEqual({ kind: 'ok', status });
    expect(await c.getTwoFactor()).toEqual({ kind: 'unauthenticated' });
  });
});
