import { createOkulClient, type InstitutionResult } from '@okul/api-client';
import { InstitutionPage } from './features/institution/InstitutionPage';

const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

// Access token in memory only — never localStorage (docs/security.md; httpOnly cookie in spec 0002).
let accessToken: string | null = null;
const client = createOkulClient({ baseUrl, getToken: () => accessToken });

async function loadInstitution(): Promise<InstitutionResult> {
  if (import.meta.env.DEV && accessToken === null) {
    const { devLogin } = await import('./auth/devLogin');
    accessToken = await devLogin(baseUrl);
  }
  return client.getMyInstitution();
}

export function App() {
  return <InstitutionPage loadInstitution={loadInstitution} />;
}
