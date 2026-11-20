import type { GuardianCodeResult, GuardianVerifyResult } from '@okul/api-client';
import { useId, useState, type FormEvent } from 'react';
import { formStyles as styles } from '../login/LoginPage';
import { guardianPhoneTexts } from './GuardianPhonePage';

/** Spec 0007, User experience — SMS kodu (AC-2, AC-3, AC-9; G-1, G-2). */
export const guardianCodeTexts = {
  title: 'SMS Kodunu Girin',
  sent: 'Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.',
  code: '6 haneli kod',
  submit: 'Doğrula',
  verifying: 'Doğrulanıyor...',
  resend: 'SMS kodunu tekrar gönder',
  cancel: 'Vazgeç',
  required: 'Bu alan gerekli.',
  invalidCode: 'Kod hatalı veya süresi geçmiş.',
  expired: 'Kodun süresi doldu. Yeni kod isteyin.',
  locked: 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
} as const;

export interface GuardianCodePageProps {
  verify: (code: string) => Promise<GuardianVerifyResult>;
  resend: () => Promise<GuardianCodeResult>;
  /** "Vazgeç": back to Veli Girişi, the fields kept (G-1). */
  onCancel: () => void;
  onLoggedIn: (consentRequired: boolean) => void;
}

export function GuardianCodePage({ verify, resend, onCancel, onLoggedIn }: GuardianCodePageProps) {
  const id = useId();
  const [code, setCode] = useState('');
  const [missing, setMissing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(guardianCodeTexts.sent);
  const [sending, setSending] = useState(false);
  const [resendBlocked, setResendBlocked] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (sending) return;
    setMissing(code.trim() === '');
    if (code.trim() === '') return;

    setSending(true);
    setError(null);
    const result = await verify(code);
    setSending(false);
    if (result.kind === 'ok') {
      onLoggedIn(result.consentRequired);
      return;
    }
    setCode('');
    setError(
      result.kind === 'invalidCode'
        ? guardianCodeTexts.invalidCode
        : result.kind === 'expired'
          ? guardianCodeTexts.expired
          : result.kind === 'locked'
            ? guardianCodeTexts.locked
            : guardianCodeTexts.network,
    );
  }

  async function again() {
    setError(null);
    const result = await resend();
    if (result.kind === 'sent') {
      setNotice(guardianCodeTexts.sent);
      return;
    }
    setNotice(null);
    if (result.kind === 'rateLimited') {
      setResendBlocked(true);
      setError(guardianPhoneTexts.rateLimited);
      return;
    }
    setError(result.kind === 'smsFailed' ? guardianPhoneTexts.smsFailed : guardianCodeTexts.network);
  }

  const inputId = `${id}-code`;
  const errorId = `${inputId}-error`;
  return (
    <main style={styles.main}>
      {notice ? (
        <p role="status" style={styles.notice}>
          {notice}
        </p>
      ) : null}
      <h1 style={styles.title}>{guardianCodeTexts.title}</h1>
      <form onSubmit={submit} noValidate>
        <div style={styles.field}>
          <label htmlFor={inputId} style={styles.label}>
            {guardianCodeTexts.code}
          </label>
          <input
            id={inputId}
            name="code"
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            aria-invalid={missing || undefined}
            aria-describedby={missing ? errorId : undefined}
            style={styles.input}
          />
          {missing ? (
            <span id={errorId} role="alert" style={styles.fieldError}>
              {guardianCodeTexts.required}
            </span>
          ) : null}
        </div>
        {error ? (
          <p role="alert" style={styles.error}>
            {error}
          </p>
        ) : null}
        <button type="submit" disabled={sending} aria-busy={sending || undefined} style={styles.button}>
          {sending ? <span role="status">{guardianCodeTexts.verifying}</span> : guardianCodeTexts.submit}
        </button>
      </form>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 16 }}>
        <button type="button" onClick={again} disabled={resendBlocked} style={linkStyle}>
          {guardianCodeTexts.resend}
        </button>
        <button type="button" onClick={onCancel} style={linkStyle}>
          {guardianCodeTexts.cancel}
        </button>
      </div>
    </main>
  );
}

export const linkStyle: React.CSSProperties = {
  background: 'none',
  border: 0,
  color: '#1d4ed8',
  fontSize: 15,
  fontWeight: 600,
  minHeight: 44,
  padding: 0,
  cursor: 'pointer',
};
