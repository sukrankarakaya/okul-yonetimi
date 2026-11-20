import { useEffect, useRef, useState } from 'react';
import { formStyles as styles } from './LoginPage';
import { codeTexts, CodeForm, secondary, type CodeOutcome } from './CodeForm';

/** Spec 0004, User experience — Kurulum (Requirement 2; AC-2, AC-13). Used at login and from Güvenlik (Öğretmen). */
export const setupTexts = {
  title: 'İki adımlı doğrulamayı kurun',
  hint: 'Doğrulama uygulamanızla QR kodunu okutun veya anahtarı elle girin.',
  key: 'Anahtar',
  copy: 'Kopyala',
  qr: 'Doğrulama uygulaması için QR kodu',
  submit: 'Kurulumu tamamla',
} as const;

export type SetupLoad = { kind: 'ok'; key: string; qrCode: string } | { kind: 'stepEnded' } | { kind: 'network' } | { kind: 'unauthenticated' };

/** The confirm's answer: on success the kurtarma kodları to show once. */
export type SetupConfirm = { kind: 'ok'; recoveryCodes: string[] } | Exclude<CodeOutcome, { kind: 'ok' }>;

export interface SetupStepProps {
  load: () => Promise<SetupLoad>;
  confirm: (code: string) => Promise<SetupConfirm>;
  /** The setup is confirmed: show these codes (Kurtarma kodları). */
  onConfirmed: (recoveryCodes: string[]) => void;
  /** The step expired or the session ended — back to where the user started. */
  onEnded: (outcome: CodeOutcome) => void;
  onCancel: () => void;
}

export function SetupStep({ load, confirm, onConfirmed, onEnded, onCancel }: SetupStepProps) {
  const [setup, setSetup] = useState<SetupLoad | null>(null);
  const codes = useRef<string[]>([]);
  const onDone = (outcome: CodeOutcome) => (outcome.kind === 'ok' ? onConfirmed(codes.current) : onEnded(outcome));
  const submit = async (code: string): Promise<CodeOutcome> => {
    const result = await confirm(code);
    if (result.kind !== 'ok') {
      return result;
    }
    codes.current = result.recoveryCodes;
    return { kind: 'ok' };
  };

  useEffect(() => {
    let active = true;
    void load().then((result) => {
      if (!active) return;
      if (result.kind === 'stepEnded' || result.kind === 'unauthenticated') {
        onEnded(result);
      } else {
        setSetup(result);
      }
    });
    return () => {
      active = false;
    };
  }, [load, onEnded]);

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{setupTexts.title}</h1>
      {setup === null ? (
        <p role="status" aria-busy="true">
          Yükleniyor…
        </p>
      ) : setup.kind === 'ok' ? (
        <CodeForm submitLabel={setupTexts.submit} submit={submit} onDone={onDone} onCancel={onCancel}>
          <p>{setupTexts.hint}</p>
          <img src={setup.qrCode} alt={setupTexts.qr} width={180} height={180} style={{ display: 'block', margin: '16px auto' }} />
          <p style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <span>{setupTexts.key}:</span>
            <code style={{ fontSize: 16, letterSpacing: 1 }}>{setup.key}</code>
            <button type="button" onClick={() => void navigator.clipboard?.writeText(setup.key)} style={{ ...secondary, width: 'auto', marginTop: 0 }}>
              {setupTexts.copy}
            </button>
          </p>
        </CodeForm>
      ) : (
        <>
          <p role="alert" style={styles.error}>
            {codeTexts.network}
          </p>
          <button type="button" onClick={onCancel} style={secondary}>
            {codeTexts.cancel}
          </button>
        </>
      )}
    </main>
  );
}
