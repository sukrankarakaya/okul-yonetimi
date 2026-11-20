import type { VerifyResult } from '@okul/api-client';
import { useState } from 'react';
import { formStyles as styles } from './LoginPage';
import { CodeForm, type CodeOutcome } from './CodeForm';

/** Spec 0004, User experience — Doğrulama (Requirement 4; AC-4, AC-6, AC-13). */
export const verifyTexts = {
  title: 'İki adımlı doğrulama',
  hint: 'Doğrulama uygulamanızdaki 6 haneli kodu girin.',
  recoveryHint: 'Kaydettiğiniz kurtarma kodlarından birini girin. Her kod bir kez kullanılır.',
  submit: 'Doğrula',
  useRecovery: 'Kurtarma kodu kullan',
  useCode: 'Doğrulama kodu kullan',
} as const;

export interface VerifyStepProps {
  verify: (code: string) => Promise<VerifyResult>;
  verifyWithRecoveryCode: (code: string) => Promise<VerifyResult>;
  onVerified: () => void;
  onStepEnded: () => void;
  onCancel: () => void;
}

export function VerifyStep({ verify, verifyWithRecoveryCode, onVerified, onStepEnded, onCancel }: VerifyStepProps) {
  const [recovery, setRecovery] = useState(false);
  const done = (outcome: CodeOutcome) => (outcome.kind === 'ok' ? onVerified() : onStepEnded());

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{verifyTexts.title}</h1>
      <p>{recovery ? verifyTexts.recoveryHint : verifyTexts.hint}</p>
      <CodeForm
        key={recovery ? 'recovery' : 'code'}
        recovery={recovery}
        submitLabel={verifyTexts.submit}
        submit={(code) => (recovery ? verifyWithRecoveryCode(code) : verify(code))}
        onDone={done}
        onCancel={onCancel}
      />
      <button type="button" onClick={() => setRecovery((r) => !r)} style={link}>
        {recovery ? verifyTexts.useCode : verifyTexts.useRecovery}
      </button>
    </main>
  );
}

export const link = {
  background: 'none',
  border: 0,
  color: '#1d4ed8',
  textDecoration: 'underline',
  fontSize: 16,
  minHeight: 44,
  padding: '0 4px',
  marginTop: 12,
  cursor: 'pointer',
} satisfies React.CSSProperties;
