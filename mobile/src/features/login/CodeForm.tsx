import { useEffect, useState, type ReactNode } from 'react';
import { AccessibilityInfo, ActivityIndicator, Platform, Pressable, Text, TextInput, View } from 'react-native';
import { formStyles as styles } from './LoginScreen';

/** Spec 0004, User experience — the texts every code step shares (same as the web panel; AC-13). */
export const codeTexts = {
  code: 'Doğrulama kodu',
  recoveryCode: 'Kurtarma kodu',
  required: 'Bu alan gerekli.',
  invalid: 'Kod hatalı veya süresi geçmiş.',
  locked: 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
  stepEnded: 'Doğrulama süresi doldu, tekrar giriş yapın.',
  cancel: 'Vazgeç',
  loading: 'Yükleniyor…',
} as const;

export type CodeOutcome =
  | { kind: 'ok' }
  | { kind: 'invalidCode' }
  | { kind: 'locked' }
  | { kind: 'network' }
  | { kind: 'stepEnded' }
  | { kind: 'unauthenticated' }
  | { kind: 'notAllowed' };

export interface CodeFormProps {
  submitLabel: string;
  recovery?: boolean;
  submit: (code: string) => Promise<CodeOutcome>;
  onDone: (outcome: CodeOutcome) => void;
  onCancel?: () => void;
  children?: ReactNode;
}

export function CodeForm({ submitLabel, recovery = false, submit, onDone, onCancel, children }: CodeFormProps) {
  const [value, setValue] = useState('');
  const [missing, setMissing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const label = recovery ? codeTexts.recoveryCode : codeTexts.code;

  useEffect(() => {
    const message = error ?? (missing ? codeTexts.required : null);
    if (message) {
      AccessibilityInfo.announceForAccessibility(message);
    }
  }, [error, missing]);

  async function send() {
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

  return (
    <View style={{ gap: 16 }}>
      {children}
      <View style={styles.field}>
        <Text nativeID="label-code" style={styles.label}>
          {label}
        </Text>
        <TextInput
          accessibilityLabel={label}
          accessibilityLabelledBy="label-code"
          value={value}
          onChangeText={setValue}
          autoCapitalize={recovery ? 'characters' : 'none'}
          autoCorrect={false}
          keyboardType={recovery ? 'default' : 'number-pad'}
          // Plan 0004, R8: the OS fills SMS codes only; an authenticator code is pasted. Set for what it can do.
          textContentType="oneTimeCode"
          autoComplete={Platform.OS === 'android' ? 'sms-otp' : 'one-time-code'}
          onSubmitEditing={() => void send()}
          style={styles.input}
        />
        {missing ? (
          <Text accessibilityLiveRegion="polite" style={styles.fieldError}>
            {codeTexts.required}
          </Text>
        ) : null}
      </View>
      {error ? (
        <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
          {error}
        </Text>
      ) : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={submitLabel}
        accessibilityState={{ disabled: sending, busy: sending }}
        disabled={sending}
        onPress={() => void send()}
        style={[styles.button, sending && styles.buttonDisabled]}
      >
        {sending ? <ActivityIndicator color="#fff" accessibilityLabel={codeTexts.loading} /> : <Text style={styles.buttonText}>{submitLabel}</Text>}
      </Pressable>
      {onCancel ? (
        <Pressable accessibilityRole="button" accessibilityLabel={codeTexts.cancel} onPress={onCancel} style={styles.secondary}>
          <Text style={styles.secondaryText}>{codeTexts.cancel}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}
