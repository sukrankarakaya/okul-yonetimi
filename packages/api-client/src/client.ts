import createClient from 'openapi-fetch';
import type { components, paths } from './schema';

export type Institution = components['schemas']['InstitutionDto'];
export type Campus = components['schemas']['CampusDto'];

/** Faz 1 roles (backend `Roles`). */
export type Role = 'Administrator' | 'Teacher' | 'Guardian' | 'Accountant';

/**
 * The outcome the screens render (spec 0001, User experience):
 * ok → success/empty · unauthenticated → "Oturum açmanız gerekiyor." · error → retry.
 */
export type InstitutionResult =
  | { kind: 'ok'; institution: Institution }
  | { kind: 'unauthenticated' }
  | { kind: 'error' };

export interface OkulClientOptions {
  baseUrl: string;
  /** Current access token, held in memory only (docs/security.md, temporary until spec 0002). */
  getToken: () => string | null;
  /** Injectable for tests. */
  fetch?: typeof globalThis.fetch;
}

export interface OkulClient {
  getMyInstitution(): Promise<InstitutionResult>;
}

export function createOkulClient(options: OkulClientOptions): OkulClient {
  const api = createClient<paths>({
    baseUrl: options.baseUrl,
    fetch: options.fetch ? (request: Request) => options.fetch!(request) : undefined,
  });

  api.use({
    onRequest({ request }) {
      const token = options.getToken();
      if (token) {
        request.headers.set('Authorization', `Bearer ${token}`);
      }
      return request;
    },
  });

  return {
    async getMyInstitution() {
      try {
        const { data, response } = await api.GET('/api/v1/institution/me');
        if (response.status === 401) {
          return { kind: 'unauthenticated' };
        }
        if (!response.ok || !data) {
          return { kind: 'error' };
        }
        return { kind: 'ok', institution: data };
      } catch {
        // Network failure / offline (spec 0001, G4): the error state, with retry.
        return { kind: 'error' };
      }
    },
  };
}

export interface DevAuthClientOptions {
  baseUrl: string;
  fetch?: typeof globalThis.fetch;
}

/**
 * Test login — Development/Testing backends only (spec 0001, Requirement 6). A separate export so
 * production bundles, which never import it, do not contain it (tree-shaken).
 */
export function createDevAuthClient(options: DevAuthClientOptions) {
  const api = createClient<paths>({
    baseUrl: options.baseUrl,
    fetch: options.fetch ? (request: Request) => options.fetch!(request) : undefined,
  });

  return {
    async testLogin(tenantId: string, role: Role): Promise<string | null> {
      try {
        const { data } = await api.POST('/api/v1/dev/test-login', { body: { tenantId, role } });
        return data?.accessToken ?? null;
      } catch {
        return null;
      }
    },
  };
}
