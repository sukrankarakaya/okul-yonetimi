import type { OkulClient, RequestResult } from '@okul/api-client';
import { createContext, useCallback, useContext, useEffect, useState } from 'react';

export interface SchoolContextValue {
  client: OkulClient;
  /** Oturum bitti: giriş ekranına dön (spec 0003, Requirement 10). */
  onSessionEnded: (expired: boolean) => void;
  toast: (message: string) => void;
}

export const SchoolContext = createContext<SchoolContextValue | null>(null);

export function useSchool(): SchoolContextValue {
  const value = useContext(SchoolContext);
  if (!value) {
    throw new Error('SchoolContext missing');
  }
  return value;
}

/** Oturumlu istek; oturum bittiyse girişe döndürür. */
export function useCall() {
  const { client, onSessionEnded } = useSchool();
  return useCallback(
    async <T,>(method: 'GET' | 'POST', path: string, body?: unknown): Promise<RequestResult<T>> => {
      const result = await client.request<T>(method, path, body);
      if (result.kind === 'unauthenticated') {
        onSessionEnded(result.expired);
      }
      return result;
    },
    [client, onSessionEnded],
  );
}

export type Loadable<T> = { kind: 'loading' } | { kind: 'ok'; data: T } | { kind: 'error'; message: string };

/** GET + yeniden yükleme. */
export function useLoad<T>(path: string | null): [Loadable<T>, () => void] {
  const call = useCall();
  const [state, setState] = useState<Loadable<T>>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (path === null) {
      return;
    }
    let active = true;
    void call<T>('GET', path).then((result) => {
      if (!active) return;
      if (result.kind === 'ok') setState({ kind: 'ok', data: result.data });
      else if (result.kind === 'invalid') setState({ kind: 'error', message: result.message });
      else if (result.kind === 'error') setState({ kind: 'error', message: 'Bilgiler alınamadı, tekrar deneyin.' });
    });
    return () => {
      active = false;
    };
  }, [call, path, attempt]);

  const reload = useCallback(() => setAttempt((n) => n + 1), []);
  return [state, reload];
}
