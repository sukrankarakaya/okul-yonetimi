import type { GuardianConsentResult } from '@okul/api-client';
import { useEffect, useId, useState } from 'react';
import { formStyles as styles } from '../login/LoginPage';

/** Spec 0007, User experience — KVKK (AC-4, AC-9). */
export const consentTexts = {
  title: 'KVKK Aydınlatması',
  agree: 'Verilerimin işlenmesine rıza veriyorum',
  submit: 'Devam et',
  hint: 'Lütfen rıza onayını işaretleyin.',
  loading: 'Yükleniyor…',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
} as const;

export interface ConsentPageProps {
  load: () => Promise<GuardianConsentResult>;
  accept: () => Promise<GuardianConsentResult>;
  onAccepted: () => void;
  onSessionEnded: () => void;
}

export function ConsentPage({ load, accept, onAccepted, onSessionEnded }: ConsentPageProps) {
  const id = useId();
  const [text, setText] = useState<string | null>(null);
  const [agreed, setAgreed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    let active = true;
    void load().then((result) => {
      if (!active) return;
      if (result.kind === 'ok') setText(result.consent.text);
      else if (result.kind === 'unauthenticated') onSessionEnded();
      else setError(consentTexts.network);
    });
    return () => {
      active = false;
    };
  }, [load, onSessionEnded]);

  async function submit() {
    if (!agreed || sending) return;
    setSending(true);
    setError(null);
    const result = await accept();
    setSending(false);
    if (result.kind === 'ok') onAccepted();
    else if (result.kind === 'unauthenticated') onSessionEnded();
    else setError(consentTexts.network);
  }

  const boxId = `${id}-agree`;
  const hintId = `${id}-hint`;
  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{consentTexts.title}</h1>
      <div
        tabIndex={0}
        aria-label={consentTexts.title}
        style={{ maxHeight: 280, overflowY: 'auto', border: '1px solid #d1d5db', borderRadius: 8, padding: 12, marginBottom: 16, lineHeight: 1.6 }}
      >
        {text ?? <span role="status">{consentTexts.loading}</span>}
      </div>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center', minHeight: 44, marginBottom: 8 }}>
        <input
          id={boxId}
          type="checkbox"
          checked={agreed}
          onChange={(e) => setAgreed(e.target.checked)}
          aria-describedby={agreed ? undefined : hintId}
          style={{ width: 22, height: 22 }}
        />
        <label htmlFor={boxId}>{consentTexts.agree}</label>
      </div>
      {!agreed ? (
        <p id={hintId} style={{ color: '#92400e', marginTop: 0 }}>
          {consentTexts.hint}
        </p>
      ) : null}
      {error ? (
        <p role="alert" style={styles.error}>
          {error}
        </p>
      ) : null}
      <button type="button" onClick={submit} disabled={!agreed || sending || text === null} style={styles.button}>
        {consentTexts.submit}
      </button>
    </main>
  );
}
