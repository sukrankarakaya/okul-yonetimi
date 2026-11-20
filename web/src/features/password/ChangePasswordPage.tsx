import type { ChangePasswordResult } from '@okul/api-client';
import { useId, useState, type FormEvent } from 'react';
import { formStyles as styles } from '../login/LoginPage';

/** Spec 0003, User experience — Şifre değiştir (AC-7, AC-10). */
export const passwordTexts = {
  title: 'Şifre değiştir',
  current: 'Mevcut şifre',
  next: 'Yeni şifre',
  nextAgain: 'Yeni şifre (tekrar)',
  submit: 'Şifreyi değiştir',
  cancel: 'Vazgeç',
  tooShort: 'Yeni şifre en az 10 karakter olmalı.',
  mismatch: 'Yeni şifreler eşleşmiyor.',
  currentWrong: 'Mevcut şifre hatalı.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
  /** Spec 0004 (B-11, plan R7): the login lock's text. */
  locked: 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.',
  changed: 'Şifreniz değiştirildi.',
} as const;

type Field = 'current' | 'next' | 'nextAgain';

export interface ChangePasswordPageProps {
  changePassword: (current: string, next: string, nextAgain: string) => Promise<ChangePasswordResult>;
  onChanged: () => void;
  onCancel: () => void;
  onSessionEnded: () => void;
}

export function ChangePasswordPage({ changePassword, onChanged, onCancel, onSessionEnded }: ChangePasswordPageProps) {
  const id = useId();
  const [values, setValues] = useState<Record<Field, string>>({ current: '', next: '', nextAgain: '' });
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (sending) {
      return;
    }
    setSending(true);
    setError(null);
    const result = await changePassword(values.current, values.next, values.nextAgain);
    setSending(false);
    const messages: Partial<Record<ChangePasswordResult, string>> = {
      tooShort: passwordTexts.tooShort,
      mismatch: passwordTexts.mismatch,
      currentWrong: passwordTexts.currentWrong,
      network: passwordTexts.network,
      locked: passwordTexts.locked,
    };
    if (result === 'changed') {
      onChanged();
    } else if (result === 'unauthenticated') {
      onSessionEnded();
    } else {
      setError(messages[result] ?? passwordTexts.network);
    }
  }

  const field = (name: Field, label: string, autoComplete: string) => (
    <div style={styles.field}>
      <label htmlFor={`${id}-${name}`} style={styles.label}>
        {label}
      </label>
      <input
        id={`${id}-${name}`}
        type="password"
        autoComplete={autoComplete}
        value={values[name]}
        onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))}
        style={styles.input}
      />
    </div>
  );

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{passwordTexts.title}</h1>
      <form onSubmit={submit} noValidate>
        {field('current', passwordTexts.current, 'current-password')}
        {field('next', passwordTexts.next, 'new-password')}
        {field('nextAgain', passwordTexts.nextAgain, 'new-password')}
        {error ? (
          <p role="alert" style={styles.error}>
            {error}
          </p>
        ) : null}
        <button type="submit" disabled={sending} aria-busy={sending || undefined} style={styles.button}>
          {sending ? <span role="status">Yükleniyor…</span> : passwordTexts.submit}
        </button>
        <button type="button" onClick={onCancel} style={{ ...styles.button, background: '#fff', color: '#1d4ed8', border: '1px solid #1d4ed8', marginTop: 12 }}>
          {passwordTexts.cancel}
        </button>
      </form>
    </main>
  );
}
