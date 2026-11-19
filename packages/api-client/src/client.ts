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
  | { kind: 'invalid' }
  | { kind: 'locked' }
  | { kind: 'network' };

export type ChangePasswordResult =
  | 'changed'
  | 'currentWrong'
  | 'tooShort'
  | 'mismatch'
  | 'unauthenticated'
  | 'network';

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
        if (response.ok && data) {
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
      const code = (error as { code?: string } | undefined)?.code;
      return code === 'current_password_wrong' ? 'currentWrong' : code === 'password_too_short' ? 'tooShort' : 'mismatch';
    },
  };
}
