import { createDevAuthClient, type Role } from '@okul/api-client';

/**
 * Development-only test login (spec 0001, Requirement 6). Imported dynamically behind `__DEV__`,
 * so production bundles never contain it; the backend route exists only in Development/Testing.
 * Defaults: the sample kurum with campuses, as Administrator.
 */
const sampleTenantWithCampuses = '019a8f00-0000-7000-8000-00000000000a';

export async function devLogin(baseUrl: string): Promise<string | null> {
  const tenantId = process.env.EXPO_PUBLIC_DEV_TENANT_ID ?? sampleTenantWithCampuses;
  const role = (process.env.EXPO_PUBLIC_DEV_ROLE ?? 'Administrator') as Role;
  return createDevAuthClient({ baseUrl }).testLogin(tenantId, role);
}
