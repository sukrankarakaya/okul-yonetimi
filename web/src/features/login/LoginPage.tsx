import type { LoginResult } from '@okul/api-client';
import { useId, useState, type FormEvent } from 'react';

/** Spec 0003, User experience — Giriş. The texts are part of the acceptance criteria (AC-10). */
export const loginTexts = {
  title: 'Giriş',
  code: 'Kurum kodu',
  email: 'E-posta',
  password: 'Şifre',
  submit: 'Giriş yap',
  required: 'Bu alan gerekli.',
  invalid: 'Kurum kodu, e-posta veya şifre hatalı.',
  locked: 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
  expired: 'Oturumunuzun süresi doldu, tekrar giriş yapın.',
  guardian: 'Veli girişi',
} as const;

type Field = 'code' | 'email' | 'password';

export interface LoginPageProps {
  login: (institutionCode: string, email: string, password: string) => Promise<LoginResult>;
  onLoggedIn: () => void;
  /** Spec 0004: the password was right; the code step (Doğrulama) or the setup (Kurulum) follows. */
  onStep?: (next: 'verify' | 'setup', step: string) => void;
  /** Shown above the form, e.g. after a session ended. */
  notice?: string;
  initialCode?: string;
  initialEmail?: string;
  /** Spec 0007: "Veli girişi" opens the veli login (Requirement 12). */
  onGuardian?: () => void;
}

export function LoginPage({ login, onLoggedIn, onStep, notice, initialCode = '', initialEmail = '', onGuardian }: LoginPageProps) {
  const id = useId();
  const [values, setValues] = useState<Record<Field, string>>({ code: initialCode, email: initialEmail, password: '' });
  const [missing, setMissing] = useState<Field[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (sending) {
      return;
    }
    const empty = (Object.keys(values) as Field[]).filter((field) => values[field].trim() === '');
    setMissing(empty);
    if (empty.length > 0) {
      return;
    }

    setSending(true);
    setError(null);
    const result = await login(values.code, values.email, values.password);
    setSending(false);
    switch (result.kind) {
      case 'ok':
        onLoggedIn();
        return;
      case 'verify':
      case 'setup':
        onStep?.(result.kind, result.step);
        return;
      case 'invalid':
        setValues((v) => ({ ...v, password: '' }));
        setError(loginTexts.invalid);
        return;
      case 'locked':
        setError(loginTexts.locked);
        return;
      case 'network':
        setError(loginTexts.network);
    }
  }

  const field = (name: Field, label: string, type: string, autoComplete: string) => {
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
          autoComplete={autoComplete}
          value={values[name]}
          onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))}
          aria-invalid={isMissing || undefined}
          aria-describedby={isMissing ? errorId : undefined}
          style={styles.input}
        />
        {isMissing ? (
          <span id={errorId} role="alert" style={styles.fieldError}>
            {loginTexts.required}
          </span>
        ) : null}
      </div>
    );
  };

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{loginTexts.title}</h1>
      {notice ? (
        <p role="status" style={styles.notice}>
          {notice}
        </p>
      ) : null}
      <form onSubmit={submit} noValidate>
        {field('code', loginTexts.code, 'text', 'organization')}
        {field('email', loginTexts.email, 'email', 'username')}
        {field('password', loginTexts.password, 'password', 'current-password')}
        {error ? (
          <p role="alert" style={styles.error}>
            {error}
          </p>
        ) : null}
        <button type="submit" disabled={sending} aria-busy={sending || undefined} style={styles.button}>
          {sending ? <span role="status">Yükleniyor…</span> : loginTexts.submit}
        </button>
      </form>
      {onGuardian ? (
        <button
          type="button"
          onClick={onGuardian}
          style={{ background: 'none', border: 0, color: '#1d4ed8', fontSize: 15, fontWeight: 600, minHeight: 44, marginTop: 16, padding: 0, cursor: 'pointer' }}
        >
          {loginTexts.guardian}
        </button>
      ) : null}
    </main>
  );
}

export const formStyles = {
  main: { fontFamily: 'system-ui, sans-serif', maxWidth: 420, margin: '48px auto', padding: 24, color: '#111' },
  title: { fontSize: 28, fontWeight: 600, marginBottom: 24 },
  field: { display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 16 },
  label: { fontWeight: 500 },
  input: { fontSize: 16, padding: '10px 12px', border: '1px solid #6b7280', borderRadius: 8, minHeight: 44 },
  fieldError: { color: '#b91c1c', fontSize: 14 },
  error: { color: '#b91c1c', background: '#fef2f2', borderRadius: 8, padding: '10px 14px' },
  notice: { color: '#1e3a8a', background: '#eff6ff', borderRadius: 8, padding: '10px 14px' },
  button: { width: '100%', background: '#1d4ed8', color: '#fff', border: 0, borderRadius: 8, padding: '12px 20px', fontSize: 16, minHeight: 44 },
} satisfies Record<string, React.CSSProperties>;

const styles = formStyles;
