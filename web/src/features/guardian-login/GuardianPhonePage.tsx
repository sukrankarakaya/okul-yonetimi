import type { GuardianCodeResult } from '@okul/api-client';
import { useId, useState, type FormEvent } from 'react';
import { formStyles as styles } from '../login/LoginPage';

/** Spec 0007, User experience — Veli Girişi (Telefon). The texts are part of the acceptance criteria (AC-9). */
export const guardianPhoneTexts = {
  title: 'Veli Girişi',
  code: 'Kurum kodu',
  phone: 'Telefon numarası',
  submit: 'SMS kodu gönder',
  sending: 'SMS kodu gönderiliyor...',
  required: 'Bu alan gerekli.',
  invalidPhone: 'Lütfen geçerli bir telefon numarası girin.',
  smsFailed: 'SMS gönderilemedi. Tekrar deneyin.',
  rateLimited: 'Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
} as const;

type Field = 'code' | 'phone';

export interface GuardianPhonePageProps {
  request: (institutionCode: string, phone: string) => Promise<GuardianCodeResult>;
  onSent: (institutionCode: string, phone: string) => void;
  notice?: string;
  initialCode?: string;
  initialPhone?: string;
}

export function GuardianPhonePage({ request, onSent, notice, initialCode = '', initialPhone = '' }: GuardianPhonePageProps) {
  const id = useId();
  const [values, setValues] = useState<Record<Field, string>>({ code: initialCode, phone: initialPhone });
  const [missing, setMissing] = useState<Field[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (sending) return;
    const empty = (Object.keys(values) as Field[]).filter((field) => values[field].trim() === '');
    setMissing(empty);
    if (empty.length > 0) return;

    setSending(true);
    setError(null);
    const result = await request(values.code, values.phone);
    setSending(false);
    switch (result.kind) {
      case 'sent':
        onSent(values.code, values.phone);
        return;
      case 'invalidPhone':
        setError(guardianPhoneTexts.invalidPhone);
        return;
      case 'rateLimited':
        setError(guardianPhoneTexts.rateLimited);
        return;
      case 'smsFailed':
        setError(guardianPhoneTexts.smsFailed);
        return;
      default:
        setError(guardianPhoneTexts.network);
    }
  }

  const field = (name: Field, label: string, type: string, inputMode: 'text' | 'tel', autoComplete: string) => {
    const inputId = `${id}-${name}`;
    const errorId = `${inputId}-error`;
    const isMissing = missing.includes(name);
    return (
      <div style={styles.field}>
        <label htmlFor={inputId} style={styles.label}>
          {label}
        </label>
        <input
          id={inputId}
          name={name}
          type={type}
          inputMode={inputMode}
          autoComplete={autoComplete}
          value={values[name]}
          onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))}
          aria-invalid={isMissing || undefined}
          aria-describedby={isMissing ? errorId : undefined}
          style={styles.input}
        />
        {isMissing ? (
          <span id={errorId} role="alert" style={styles.fieldError}>
            {guardianPhoneTexts.required}
          </span>
        ) : null}
      </div>
    );
  };

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{guardianPhoneTexts.title}</h1>
      {notice ? (
        <p role="status" style={styles.notice}>
          {notice}
        </p>
      ) : null}
      <form onSubmit={submit} noValidate>
        {field('code', guardianPhoneTexts.code, 'text', 'text', 'organization')}
        {field('phone', guardianPhoneTexts.phone, 'tel', 'tel', 'tel')}
        {error ? (
          <p role="alert" style={styles.error}>
            {error}
          </p>
        ) : null}
        <button type="submit" disabled={sending} aria-busy={sending || undefined} style={styles.button}>
          {sending ? <span role="status">{guardianPhoneTexts.sending}</span> : guardianPhoneTexts.submit}
        </button>
      </form>
    </main>
  );
}
