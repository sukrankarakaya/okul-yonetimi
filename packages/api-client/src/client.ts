import createClient from 'openapi-fetch';
import type { components, paths } from './schema';

export type Institution = components['schemas']['InstitutionDto'];
export type Campus = components['schemas']['CampusDto'];

/** Faz 1 roles (backend `Roles`). */
export type Role = 'Administrator' | 'Teacher' | 'Guardian' | 'Accountant';

/**
 * The outcome the Kurum bilgisi screen renders (spec 0001, User experience; spec 0003):
 * ok → success/empty · unauthenticated → back to Giriş (`expired`: "Oturumunuzun süresi doldu…") · error → retry.
 */
export type InstitutionResult =
  | { kind: 'ok'; institution: Institution }
  | { kind: 'unauthenticated'; expired: boolean }
  | { kind: 'error' };

/** Giriş (spec 0003, User experience): one answer per state, never which field was wrong. */
export type LoginResult =
  | { kind: 'ok'; roles: string[] }
  /** Spec 0004: the password was right; a code completes the login (Doğrulama). */
  | { kind: 'verify'; step: string }
  /** Spec 0004: the password was right; 2FA must be set up first (Kurulum). */
  | { kind: 'setup'; step: string }
  | { kind: 'invalid' }
  | { kind: 'locked' }
  | { kind: 'network' };

export type ChangePasswordResult =
  | 'changed'
  | 'currentWrong'
  | 'tooShort'
  | 'mismatch'
  /** Spec 0004 (B-11): wrong current passwords count towards the login lock. */
  | 'locked'
  | 'unauthenticated'
  | 'network';

/** A code step (Doğrulama, Kurulum onayı, Güvenlik): the spec 0004 answers. */
export type CodeRefusal = { kind: 'invalidCode' } | { kind: 'locked' } | { kind: 'stepEnded' } | { kind: 'network' };

export type VerifyResult = { kind: 'ok'; roles: string[] } | CodeRefusal;

export type SetupStartResult = { kind: 'ok'; key: string; qrCode: string } | { kind: 'stepEnded' } | { kind: 'network' };

export type SetupConfirmResult = { kind: 'ok'; roles: string[]; recoveryCodes: string[] } | CodeRefusal;

export type TwoFactorStatus = components['schemas']['TwoFactorStatusResponse'];

export type TwoFactorStatusResult = { kind: 'ok'; status: TwoFactorStatus } | { kind: 'unauthenticated' } | { kind: 'network' };

export type EnableStartResult =
  | { kind: 'ok'; step: string; key: string; qrCode: string }
  | { kind: 'unauthenticated' }
  | { kind: 'network' };

/** Güvenlik changes: `recoveryCodes` when new ones were made; `notAllowed` = mandatory for the role. */
export type TwoFactorChangeResult =
  | { kind: 'ok'; recoveryCodes?: string[] }
  | CodeRefusal
  | { kind: 'notAllowed' }
  | { kind: 'unauthenticated' };

/** Mobile only: where access + refresh live — the device's secure store (spec 0003, Requirement 8). */
export interface TokenStore {
  read(): Promise<StoredTokens | null>;
  write(tokens: StoredTokens): Promise<void>;
  clear(): Promise<void>;
}

export interface StoredTokens {
  accessToken: string;
  refreshToken: string;
}

export interface OkulClientOptions {
  baseUrl: string;
  /**
   * `web`: the session lives in httpOnly cookies the page cannot read; every request sends them and the CSRF header.
   * `mobile`: bearer tokens kept in `tokenStore`.
   */
  kind: 'web' | 'mobile';
  tokenStore?: TokenStore;
  /** Web: serializes refresh across tabs (Web Locks — plan 0003, R5). Default: no cross-tab lock. */
  lock?: <T>(name: string, work: () => Promise<T>) => Promise<T>;
  /** Injectable for tests. */
  fetch?: typeof globalThis.fetch;
}

export interface OkulClient {
  login(institutionCode: string, email: string, password: string): Promise<LoginResult>;
  logout(): Promise<void>;
  /** Is there a usable session? Renews it silently when only the 15-minute access has run out. */
  hasSession(): Promise<boolean>;
  getMyInstitution(): Promise<InstitutionResult>;
  changePassword(current: string, next: string, nextAgain: string): Promise<ChangePasswordResult>;
  /** Spec 0004 — the login step: a 6-digit code, or a kurtarma kodu instead. */
  verifyLogin(step: string, code: string): Promise<VerifyResult>;
  verifyWithRecoveryCode(step: string, recoveryCode: string): Promise<VerifyResult>;
  startSetup(step: string): Promise<SetupStartResult>;
  confirmSetup(step: string, code: string): Promise<SetupConfirmResult>;
  /** "Vazgeç": the step can never be used again. Never fails. */
  cancelLogin(step: string): Promise<void>;
  getTwoFactor(): Promise<TwoFactorStatusResult>;
  startEnableTwoFactor(): Promise<EnableStartResult>;
  confirmEnableTwoFactor(step: string, code: string): Promise<TwoFactorChangeResult>;
  disableTwoFactor(code: string): Promise<TwoFactorChangeResult>;
  renewRecoveryCodes(code: string): Promise<TwoFactorChangeResult>;
}

/** The header the API requires on cookie-authenticated state-changing requests (backend `SessionCookies`). */
export const csrfHeader = 'X-Okul-Csrf';

type Refreshed = 'renewed' | 'ended' | 'network';

export function createOkulClient(options: OkulClientOptions): OkulClient {
  if (options.kind === 'mobile' && !options.tokenStore) {
    throw new Error('The mobile client needs a tokenStore.');
  }

  const api = createClient<paths>({
    baseUrl: options.baseUrl,
    fetch: options.fetch ? (request: Request) => options.fetch!(request) : undefined,
    credentials: options.kind === 'web' ? 'include' : 'omit',
  });

  api.use({
    async onRequest({ request }) {
      if (options.kind === 'web') {
        request.headers.set(csrfHeader, '1');
      } else if (!request.url.includes('/api/v1/auth/login') && !request.url.includes('/api/v1/auth/refresh')) {
        const tokens = await options.tokenStore!.read();
        if (tokens) {
          request.headers.set('Authorization', `Bearer ${tokens.accessToken}`);
        }
      }
      return request;
    },
  });

  const lock = options.lock ?? (<T>(_: string, work: () => Promise<T>) => work());

  // Single flight: requests that need a renewal at the same time share ONE refresh (spec 0003, AC-5).
  let refreshing: Promise<Refreshed> | null = null;

  function refresh(): Promise<Refreshed> {
    refreshing ??= lock('okul-refresh', doRefresh).finally(() => {
      refreshing = null;
    });
    return refreshing;
  }

  async function doRefresh(): Promise<Refreshed> {
    try {
      let body: { refreshToken: string } | undefined;
      if (options.kind === 'mobile') {
        const tokens = await options.tokenStore!.read();
        if (!tokens) {
          return 'ended';
        }
        body = { refreshToken: tokens.refreshToken };
      }

      const { data, response } = await api.POST('/api/v1/auth/refresh', { body });
      if (response.status === 409) {
        return 'renewed'; // another tab/request renewed it a moment ago (grace window) — just retry
      }
      if (!response.ok || !data) {
        await forget();
        return 'ended';
      }
      await remember(data);
      return 'renewed';
    } catch {
      return 'network';
    }
  }

  async function remember(session: components['schemas']['SessionResponse']) {
    if (options.kind === 'mobile' && session.accessToken && session.refreshToken) {
      await options.tokenStore!.write({ accessToken: session.accessToken, refreshToken: session.refreshToken });
    }
  }

  async function forget() {
    if (options.kind === 'mobile') {
      await options.tokenStore!.clear();
    }
  }

  /** Runs a request; on 401 renews once and retries. `ended` = the session is over, the user logs in again. */
  async function withSession<T extends { response: Response }>(
    send: () => Promise<T>,
  ): Promise<{ result: T } | { ended: true; hadSession: boolean } | { network: true }> {
    try {
      const first = await send();
      if (first.response.status !== 401) {
        return { result: first };
      }

      const hadSession = options.kind === 'web' || (await options.tokenStore!.read()) !== null;
      const renewed = await refresh();
      if (renewed === 'network') {
        return { network: true };
      }
      if (renewed === 'ended') {
        return { ended: true, hadSession };
      }

      const second = await send();
      if (second.response.status === 401) {
        await forget();
        return { ended: true, hadSession };
      }
      return { result: second };
    } catch {
      return { network: true };
    }
  }

  return {
    async login(institutionCode, email, password) {
      try {
        const { data, response } = await api.POST('/api/v1/auth/login', {
          body: { institutionCode, email, password, client: options.kind },
        });
        if (response.status === 202 && data && 'next' in data) {
          return data.next === 'setup' ? { kind: 'setup', step: data.step } : { kind: 'verify', step: data.step };
        }
        if (response.ok && data && 'roles' in data) {
          await remember(data);
          return { kind: 'ok', roles: data.roles };
        }
        // Only 401 is "wrong input" and only 429 is "locked"; a server error or a refused request is not the user's
        // mistake — it must not clear their password with "hatalı bilgi" (spec 0003 review round 1 #7).
        if (response.status === 401) {
          return { kind: 'invalid' };
        }
        return response.status === 429 ? { kind: 'locked' } : { kind: 'network' };
      } catch {
        return { kind: 'network' };
      }
    },

    async logout() {
      try {
        await withSession(() => api.POST('/api/v1/auth/logout'));
      } finally {
        await forget();
      }
    },

    async hasSession() {
      if (options.kind === 'mobile' && (await options.tokenStore!.read()) === null) {
        return false;
      }
      const outcome = await withSession(() => api.GET('/api/v1/auth/session'));
      return 'result' in outcome && outcome.result.response.ok;
    },

    async getMyInstitution() {
      const outcome = await withSession(() => api.GET('/api/v1/institution/me'));
      if ('network' in outcome) {
        // Network failure / offline (spec 0001, G4): the error state, with retry.
        return { kind: 'error' };
      }
      if ('ended' in outcome) {
        return { kind: 'unauthenticated', expired: outcome.hadSession };
      }
      const { data, response } = outcome.result;
      if (!response.ok || !data) {
        return { kind: 'error' };
      }
      return { kind: 'ok', institution: data };
    },

    async changePassword(current, next, nextAgain) {
      const outcome = await withSession(() =>
        api.POST('/api/v1/auth/password', {
          body: { currentPassword: current, newPassword: next, newPasswordAgain: nextAgain },
        }),
      );
      if ('network' in outcome) {
        return 'network';
      }
      if ('ended' in outcome) {
        return 'unauthenticated';
      }
      const { response, error } = outcome.result;
      if (response.ok) {
        return 'changed';
      }
      // Only the three 400 codes are the user's input; a server error, a refusal (403/429) or an unknown answer is not —
      // it must not read "Yeni şifreler eşleşmiyor." (spec 0003 VERIFY round 1 F-2, the twin of review round 1 #7).
      const code = (error as { code?: string } | undefined)?.code;
      if (response.status === 400) {
        if (code === 'current_password_wrong') return 'currentWrong';
        if (code === 'password_too_short') return 'tooShort';
        if (code === 'password_mismatch') return 'mismatch';
      }
      return response.status === 429 ? 'locked' : 'network';
    },

    async verifyLogin(step, code) {
      return verify({ step, code, recoveryCode: null });
    },

    async verifyWithRecoveryCode(step, recoveryCode) {
      return verify({ step, code: null, recoveryCode });
    },

    async startSetup(step) {
      try {
        const { data, response } = await api.POST('/api/v1/auth/login/setup', { body: { step } });
        if (response.ok && data) {
          return { kind: 'ok', key: data.key, qrCode: data.qrCode };
        }
        return response.status === 401 ? { kind: 'stepEnded' } : { kind: 'network' };
      } catch {
        return { kind: 'network' };
      }
    },

    async confirmSetup(step, code) {
      try {
        const { data, response, error } = await api.POST('/api/v1/auth/login/setup/confirm', { body: { step, code, recoveryCode: null } });
        if (response.ok && data) {
          await remember(data.session);
          return { kind: 'ok', roles: data.session.roles, recoveryCodes: data.recoveryCodes };
        }
        return refusal(response, error);
      } catch {
        return { kind: 'network' };
      }
    },

    async cancelLogin(step) {
      try {
        await api.POST('/api/v1/auth/login/cancel', { body: { step } });
      } catch {
        // "Vazgeç" always returns to Giriş; an unreachable server lets the step expire on its own (5 minutes).
      }
    },

    async getTwoFactor() {
      const outcome = await withSession(() => api.GET('/api/v1/auth/two-factor'));
      if ('network' in outcome) return { kind: 'network' };
      if ('ended' in outcome) return { kind: 'unauthenticated' };
      const { data, response } = outcome.result;
      return response.ok && data ? { kind: 'ok', status: data } : { kind: 'network' };
    },

    async startEnableTwoFactor() {
      const outcome = await withSession(() => api.POST('/api/v1/auth/two-factor/enable'));
      if ('network' in outcome) return { kind: 'network' };
      if ('ended' in outcome) return { kind: 'unauthenticated' };
      const { data, response } = outcome.result;
      return response.ok && data ? { kind: 'ok', step: data.step, key: data.key, qrCode: data.qrCode } : { kind: 'network' };
    },

    async confirmEnableTwoFactor(step, code) {
      return change(() => api.POST('/api/v1/auth/two-factor/enable/confirm', { body: { step, code, recoveryCode: null } }));
    },

    async disableTwoFactor(code) {
      return change(() => api.POST('/api/v1/auth/two-factor/disable', { body: { code } }));
    },

    async renewRecoveryCodes(code) {
      return change(() => api.POST('/api/v1/auth/two-factor/recovery-codes', { body: { code } }));
    },
  };

  async function verify(body: { step: string; code: string | null; recoveryCode: string | null }): Promise<VerifyResult> {
    try {
      const { data, response, error } = await api.POST('/api/v1/auth/login/verify', { body });
      if (response.ok && data) {
        await remember(data);
        return { kind: 'ok', roles: data.roles };
      }
      return refusal(response, error);
    } catch {
      return { kind: 'network' };
    }
  }

  /**
   * Only the API's own codes are the user's: `invalid_code` → "Kod hatalı…", `step_ended` → back to Giriş, 429 → locked.
   * Anything else (5xx, an unknown answer) is "Bağlantı kurulamadı" — never "Kod hatalı" (spec 0003 review round 1 #7).
   */
  function refusal(response: Response, error: unknown): CodeRefusal {
    const code = (error as { code?: string } | undefined)?.code;
    if (response.status === 429) return { kind: 'locked' };
    if (code === 'invalid_code') return { kind: 'invalidCode' };
    if (code === 'step_ended') return { kind: 'stepEnded' };
    return { kind: 'network' };
  }

  async function change<T extends { response: Response; data?: unknown; error?: unknown }>(
    send: () => Promise<T>,
  ): Promise<TwoFactorChangeResult> {
    const outcome = await withSession(send);
    if ('network' in outcome) return { kind: 'network' };
    if ('ended' in outcome) return { kind: 'unauthenticated' };
    const { response, data, error } = outcome.result;
    if (response.ok) {
      const codes = (data as { recoveryCodes?: string[] } | undefined)?.recoveryCodes;
      return codes ? { kind: 'ok', recoveryCodes: codes } : { kind: 'ok' };
    }
    if (response.status === 403) return { kind: 'notAllowed' };
    return refusal(response, error);
  }
}
