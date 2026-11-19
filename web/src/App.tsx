import { createOkulClient, type OkulClient } from '@okul/api-client';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { LoginPage, loginTexts } from './features/login/LoginPage';
import { ChangePasswordPage, passwordTexts } from './features/password/ChangePasswordPage';
import { SchoolShell } from './features/school/SchoolShell';

const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

/**
 * Web panel (spec 0003). The session lives in httpOnly cookies the page cannot read — nothing in localStorage or
 * sessionStorage (Requirement 8). Tabs share it; a refresh is serialized across tabs with Web Locks (plan 0003, R5).
 * There is no automatic test login any more (Requirement 9): the app opens on Giriş without a session.
 */
export function createWebClient(fetchImpl?: typeof fetch): OkulClient {
  return createOkulClient({
    baseUrl,
    kind: 'web',
    fetch: fetchImpl,
    lock: typeof navigator !== 'undefined' && navigator.locks
      ? <T,>(name: string, work: () => Promise<T>) => navigator.locks.request(name, work) as Promise<T>
      : undefined,
  });
}

type Screen =
  | { kind: 'checking' }
  | { kind: 'login'; notice?: string }
  | { kind: 'institution'; notice?: string }
  | { kind: 'password' };

export function App({ client: injected }: { client?: OkulClient } = {}) {
  const client = useMemo(() => injected ?? createWebClient(), [injected]);
  const [screen, setScreen] = useState<Screen>({ kind: 'checking' });

  useEffect(() => {
    let active = true;
    void client.hasSession().then((has) => {
      if (active) {
        setScreen(has ? { kind: 'institution' } : { kind: 'login' });
      }
    });
    return () => {
      active = false;
    };
  }, [client]);

  const loadInstitution = useCallback(() => client.getMyInstitution(), [client]);
  const sessionEnded = useCallback(
    (expired: boolean) => setScreen({ kind: 'login', notice: expired ? loginTexts.expired : undefined }),
    [],
  );

  switch (screen.kind) {
    case 'checking':
      return (
        <p role="status" aria-busy="true" style={{ fontFamily: 'system-ui, sans-serif', padding: 24 }}>
          Yükleniyor…
        </p>
      );
    case 'login':
      return (
        <LoginPage
          login={(code, email, password) => client.login(code, email, password)}
          onLoggedIn={() => setScreen({ kind: 'institution' })}
          notice={screen.notice}
        />
      );
    case 'password':
      return (
        <ChangePasswordPage
          changePassword={(current, next, again) => client.changePassword(current, next, again)}
          onChanged={() => setScreen({ kind: 'institution', notice: passwordTexts.changed })}
          onCancel={() => setScreen({ kind: 'institution' })}
          onSessionEnded={() => sessionEnded(true)}
        />
      );
    case 'institution':
      // Demo modu: giriş sonrası okul paneli (kurum adı başlıkta, sekmeler: panel, öğrenciler, yoklama, …).
      return (
        <SchoolShell
          client={client}
          loadInstitution={loadInstitution}
          notice={screen.notice}
          onSessionEnded={sessionEnded}
          onChangePassword={() => setScreen({ kind: 'password' })}
          onLogout={() => {
            void client.logout().finally(() => setScreen({ kind: 'login' }));
          }}
        />
      );
  }
}
