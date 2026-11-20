import type { InstitutionResult } from '@okul/api-client';
import { useCallback, useEffect, useLayoutEffect, useState } from 'react';

/** Spec 0001 + 0003, User experience — the texts are part of the acceptance criteria. */
export const texts = {
  loading: 'Yükleniyor…',
  empty: 'Henüz şube tanımlanmamış.',
  error: 'Bilgiler alınamadı, tekrar deneyin.',
  retry: 'Tekrar dene',
  campusList: 'Şubeler',
  logout: 'Çıkış yap',
  changePassword: 'Şifremi değiştir',
  defaultTitle: 'Okul',
} as const;

type PageState = { kind: 'loading' } | InstitutionResult;

export interface InstitutionPageProps {
  loadInstitution: () => Promise<InstitutionResult>;
  /** No (or an ended) session: back to Giriş — never kurum data (spec 0003, Requirement 10). */
  onSessionEnded?: (expired: boolean) => void;
  onLogout?: () => void;
  onChangePassword?: () => void;
  /** A one-off message above the page, e.g. "Şifreniz değiştirildi." */
  notice?: string;
}

export function InstitutionPage({ loadInstitution, onSessionEnded, onLogout, onChangePassword, notice }: InstitutionPageProps) {
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    void loadInstitution().then((result) => {
      if (active) {
        setState(result);
      }
    });
    return () => {
      active = false;
    };
  }, [loadInstitution, attempt]);

  useEffect(() => {
    if (state.kind === 'unauthenticated') {
      onSessionEnded?.(state.expired);
    }
  }, [state, onSessionEnded]);

  // The kurum name is the page title (accessibility). Layout effect: the title changes in the same
  // commit as the heading, never one paint later (plan 0001, M-3 — the race a test caught).
  useLayoutEffect(() => {
    document.title = state.kind === 'ok' ? state.institution.name : texts.defaultTitle;
  }, [state]);

  const retry = useCallback(() => {
    setState({ kind: 'loading' });
    setAttempt((n) => n + 1);
  }, []);

  return (
    <main style={styles.main}>
      {notice ? (
        <p role="status" style={styles.notice}>
          {notice}
        </p>
      ) : null}
      {renderState(state, retry)}
      {state.kind === 'ok' && (onLogout || onChangePassword) ? (
        <nav aria-label="Hesap" style={styles.actions}>
          {onChangePassword ? (
            <button type="button" onClick={onChangePassword} style={styles.secondary}>
              {texts.changePassword}
            </button>
          ) : null}
          {onLogout ? (
            <button type="button" onClick={onLogout} style={styles.secondary}>
              {texts.logout}
            </button>
          ) : null}
        </nav>
      ) : null}
    </main>
  );
}

function renderState(state: PageState, retry: () => void) {
  switch (state.kind) {
    case 'loading':
      return (
        <p role="status" aria-live="polite" aria-busy="true">
          {texts.loading}
        </p>
      );

    case 'unauthenticated':
      return null; // the app shows Giriş (spec 0003); nothing of the kurum is rendered

    case 'error':
      return (
        <div>
          <p role="alert">{texts.error}</p>
          <button type="button" onClick={retry} style={styles.button}>
            {texts.retry}
          </button>
        </div>
      );

    case 'ok': {
      const { institution } = state;
      return (
        <>
          {/* React renders strings as text — names are never interpreted as markup (spec 0001, G6). */}
          <h1 style={styles.title}>{institution.name}</h1>
          {institution.campuses.length === 0 ? (
            <p role="status">{texts.empty}</p>
          ) : (
            <ul aria-label={texts.campusList} style={styles.list}>
              {institution.campuses.map((campus) => (
                <li key={campus.id} style={styles.item}>
                  {campus.name}
                </li>
              ))}
            </ul>
          )}
        </>
      );
    }
  }
}

const styles = {
  main: { fontFamily: 'system-ui, sans-serif', maxWidth: 640, margin: '0 auto', padding: 24, color: '#111' },
  title: { fontSize: 28, fontWeight: 600, overflowWrap: 'anywhere' },
  list: { listStyle: 'none', padding: 0 },
  item: { padding: '8px 0', borderBottom: '1px solid #ddd', overflowWrap: 'anywhere' },
  button: { background: '#1d4ed8', color: '#fff', border: 0, borderRadius: 8, padding: '10px 20px', fontSize: 16, minHeight: 44 },
  secondary: { background: '#fff', color: '#1d4ed8', border: '1px solid #1d4ed8', borderRadius: 8, padding: '10px 16px', fontSize: 16, minHeight: 44 },
  actions: { display: 'flex', gap: 12, marginTop: 24, flexWrap: 'wrap' },
  notice: { background: '#ecfdf5', color: '#065f46', borderRadius: 8, padding: '10px 14px' },
} satisfies Record<string, React.CSSProperties>;
