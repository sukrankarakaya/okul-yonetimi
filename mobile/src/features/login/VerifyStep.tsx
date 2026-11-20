import type { VerifyResult } from '@okul/api-client';
import { useState } from 'react';
import { Pressable, ScrollView, Text } from 'react-native';
import { formStyles as styles } from './LoginScreen';
import { CodeForm, type CodeOutcome } from './CodeForm';

/** Spec 0004, User experience — Doğrulama (same texts as the web panel). */
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
  const toggle = recovery ? verifyTexts.useCode : verifyTexts.useRecovery;

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text accessibilityRole="header" style={styles.title}>
        {verifyTexts.title}
      </Text>
      <Text style={styles.label}>{recovery ? verifyTexts.recoveryHint : verifyTexts.hint}</Text>
      <CodeForm
        key={recovery ? 'recovery' : 'code'}
        recovery={recovery}
        submitLabel={verifyTexts.submit}
        submit={(code) => (recovery ? verifyWithRecoveryCode(code) : verify(code))}
        onDone={done}
        onCancel={onCancel}
      />
      <Pressable accessibilityRole="button" accessibilityLabel={toggle} onPress={() => setRecovery((r) => !r)} style={{ minHeight: 44, justifyContent: 'center' }}>
        <Text style={styles.secondaryText}>{toggle}</Text>
      </Pressable>
    </ScrollView>
  );
}
