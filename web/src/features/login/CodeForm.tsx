import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { formStyles as styles } from './LoginPage';

/** Spec 0004, User experience — the texts every code step shares (AC-13). */
export const codeTexts = {
  code: 'Doğrulama kodu',
  recoveryCode: 'Kurtarma kodu',
  required: 'Bu alan gerekli.',
  invalid: 'Kod hatalı veya süresi geçmiş.',
  locked: 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
  stepEnded: 'Doğrulama süresi doldu, tekrar giriş yapın.',
  cancel: 'Vazgeç',
} as const;

/** What a code submission can answer; the form shows the refusals, the caller handles the rest. */
export type CodeOutcome =
  | { kind: 'ok' }
  | { kind: 'invalidCode' }
  | { kind: 'locked' }
  | { kind: 'network' }
  | { kind: 'stepEnded' }
  | { kind: 'unauthenticated' }
  | { kind: 'notAllowed' };

export interface CodeFormProps {
  label?: string;
  submitLabel: string;
  /** Recovery codes are typed as letters; authenticator codes as digits (numeric keyboard). */
  recovery?: boolean;
  submit: (code: string) => Promise<CodeOutcome>;
  /** Anything other than a refusal the form can show: ok, stepEnded, unauthenticated, notAllowed. */
  onDone: (outcome: CodeOutcome) => void;
  onCancel?: () => void;
  children?: ReactNode;
}

export function CodeForm({ label, submitLabel, recovery = false, submit, onDone, onCancel, children }: CodeFormProps) {
  const id = useId();
  const [value, setValue] = useState('');
  const [missing, setMissing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (sending) {
      return;
    }
    if (value.trim() === '') {
      setMissing(true);
      return;
    }

    setMissing(false);
    setSending(true);
    setError(null);
    const outcome = await submit(value);
    setSending(false);
    switch (outcome.kind) {
      case 'invalidCode':
        setValue('');
        setError(codeTexts.invalid);
        return;
      case 'locked':
        setError(codeTexts.locked);
        return;
      case 'network':
        setError(codeTexts.network);
        return;
      default:
        onDone(outcome);
    }
  }

  const inputId = `${id}-code`;
  const errorId = `${inputId}-error`;
  return (
    <form onSubmit={onSubmit} noValidate>
      {children}
      <div style={styles.field}>
        <label htmlFor={inputId} style={styles.label}>
          {label ?? (recovery ? codeTexts.recoveryCode : codeTexts.code)}
        </label>
        <input
          id={inputId}
          name="code"
          type="text"
          inputMode={recovery ? 'text' : 'numeric'}
          autoComplete="one-time-code"
          autoCapitalize={recovery ? 'characters' : undefined}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          aria-invalid={missing || undefined}
          aria-describedby={missing ? errorId : undefined}
          style={styles.input}
        />
        {missing ? (
          <span id={errorId} role="alert" style={styles.fieldError}>
            {codeTexts.required}
          </span>
        ) : null}
      </div>
      {error ? (
        <p role="alert" style={styles.error}>
          {error}
        </p>
      ) : null}
      <button type="submit" disabled={sending} aria-busy={sending || undefined} style={styles.button}>
        {sending ? <span role="status">Yükleniyor…</span> : submitLabel}
      </button>
      {onCancel ? (
        <button type="button" onClick={onCancel} style={secondary}>
          {codeTexts.cancel}
        </button>
      ) : null}
    </form>
  );
}

export const secondary = {
  ...styles.button,
  background: '#fff',
  color: '#1d4ed8',
  border: '1px solid #1d4ed8',
  marginTop: 12,
} satisfies React.CSSProperties;
