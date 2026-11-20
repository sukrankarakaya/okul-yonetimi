import type { LoginResult } from '@okul/api-client';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

/** Spec 0003, User experience — Giriş (same texts as the web panel). */
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
  loading: 'Yükleniyor…',
} as const;

type Field = 'code' | 'email' | 'password';

export interface LoginScreenProps {
  login: (institutionCode: string, email: string, password: string) => Promise<LoginResult>;
  onLoggedIn: (institutionCode: string, email: string) => void;
  /** Spec 0004: the password was right; Doğrulama or Kurulum follows (the fields are remembered as for a login). */
  onStep?: (next: 'verify' | 'setup', step: string, institutionCode: string, email: string) => void;
  notice?: string;
  initialCode?: string;
  initialEmail?: string;
}

export function LoginScreen({ login, onLoggedIn, onStep, notice, initialCode = '', initialEmail = '' }: LoginScreenProps) {
  const [values, setValues] = useState<Record<Field, string>>({ code: initialCode, email: initialEmail, password: '' });
  const [missing, setMissing] = useState<Field[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    // iOS VoiceOver needs an explicit announcement; Android also gets accessibilityLiveRegion (spec 0001, M-1).
    const message = error ?? (missing.length > 0 ? loginTexts.required : notice);
    if (message) {
      AccessibilityInfo.announceForAccessibility(message);
    }
  }, [error, missing, notice]);

  async function submit() {
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
        onLoggedIn(values.code.trim(), values.email.trim());
        return;
      case 'verify':
      case 'setup':
        onStep?.(result.kind, result.step, values.code.trim(), values.email.trim());
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

  const field = (name: Field, label: string, options: Partial<React.ComponentProps<typeof TextInput>>) => (
    <View style={styles.field}>
      <Text nativeID={`label-${name}`} style={styles.label}>
        {label}
      </Text>
      <TextInput
        accessibilityLabel={label}
        accessibilityLabelledBy={`label-${name}`}
        value={values[name]}
        onChangeText={(text) => setValues((v) => ({ ...v, [name]: text }))}
        autoCapitalize="none"
        autoCorrect={false}
        style={styles.input}
        {...options}
      />
      {missing.includes(name) ? (
        <Text accessibilityLiveRegion="polite" style={styles.fieldError}>
          {loginTexts.required}
        </Text>
      ) : null}
    </View>
  );

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text accessibilityRole="header" style={styles.title}>
        {loginTexts.title}
      </Text>
      {notice ? (
        <Text accessibilityLiveRegion="polite" style={styles.notice}>
          {notice}
        </Text>
      ) : null}
      {field('code', loginTexts.code, { autoComplete: 'organization', textContentType: 'organizationName' })}
      {field('email', loginTexts.email, { autoComplete: 'username', textContentType: 'username', keyboardType: 'email-address' })}
      {field('password', loginTexts.password, {
        autoComplete: 'current-password',
        textContentType: 'password',
        secureTextEntry: true,
        onSubmitEditing: () => void submit(),
      })}
      {error ? (
        <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
          {error}
        </Text>
      ) : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={loginTexts.submit}
        accessibilityState={{ disabled: sending, busy: sending }}
        disabled={sending}
        onPress={() => void submit()}
        style={[styles.button, sending && styles.buttonDisabled]}
      >
        {sending ? <ActivityIndicator color="#fff" accessibilityLabel={loginTexts.loading} /> : <Text style={styles.buttonText}>{loginTexts.submit}</Text>}
      </Pressable>
    </ScrollView>
  );
}

export const formStyles = StyleSheet.create({
  content: { padding: 24, gap: 16 },
  title: { fontSize: 24, fontWeight: '600', color: '#111' },
  field: { gap: 6 },
  label: { fontSize: 16, fontWeight: '500', color: '#111' },
  input: { minHeight: 44, borderWidth: 1, borderColor: '#6b7280', borderRadius: 8, paddingHorizontal: 12, fontSize: 16, color: '#111' },
  fieldError: { color: '#b91c1c', fontSize: 14 },
  error: { color: '#b91c1c', backgroundColor: '#fef2f2', borderRadius: 8, padding: 12, fontSize: 16 },
  notice: { color: '#1e3a8a', backgroundColor: '#eff6ff', borderRadius: 8, padding: 12, fontSize: 16 },
  button: { minHeight: 44, backgroundColor: '#1d4ed8', borderRadius: 8, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 20 },
  buttonDisabled: { opacity: 0.7 },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
  secondary: { minHeight: 44, borderWidth: 1, borderColor: '#1d4ed8', borderRadius: 8, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 20 },
  secondaryText: { color: '#1d4ed8', fontSize: 16, fontWeight: '600' },
});

const styles = formStyles;
