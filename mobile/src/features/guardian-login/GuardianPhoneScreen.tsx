import type { GuardianCodeResult } from '@okul/api-client';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { formStyles as styles } from '../login/LoginScreen';

/** Spec 0007, User experience — Veli Girişi (same texts as the web panel; AC-9). */
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

export interface GuardianPhoneScreenProps {
  request: (institutionCode: string, phone: string) => Promise<GuardianCodeResult>;
  /** The trimmed kurum kodu — the app remembers it (G-5); the telefon is never stored. */
  onSent: (institutionCode: string, phone: string) => void;
  notice?: string;
  initialCode?: string;
  initialPhone?: string;
}

export function GuardianPhoneScreen({ request, onSent, notice, initialCode = '', initialPhone = '' }: GuardianPhoneScreenProps) {
  const [values, setValues] = useState<Record<Field, string>>({ code: initialCode, phone: initialPhone });
  const [missing, setMissing] = useState<Field[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    const message = error ?? (missing.length > 0 ? guardianPhoneTexts.required : notice);
    if (message) AccessibilityInfo.announceForAccessibility(message);
  }, [error, missing, notice]);

  async function submit() {
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
        onSent(values.code.trim(), values.phone);
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

  const field = (name: Field, label: string, options: Partial<React.ComponentProps<typeof TextInput>>) => (
    <View style={styles.field}>
      <Text nativeID={`guardian-${name}`} style={styles.label}>
        {label}
      </Text>
      <TextInput
        accessibilityLabel={label}
        accessibilityLabelledBy={`guardian-${name}`}
        value={values[name]}
        onChangeText={(text) => setValues((v) => ({ ...v, [name]: text }))}
        autoCapitalize="none"
        autoCorrect={false}
        style={styles.input}
        {...options}
      />
      {missing.includes(name) ? (
        <Text accessibilityLiveRegion="polite" style={styles.fieldError}>
          {guardianPhoneTexts.required}
        </Text>
      ) : null}
    </View>
  );

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text accessibilityRole="header" style={styles.title}>
        {guardianPhoneTexts.title}
      </Text>
      {notice ? (
        <Text accessibilityLiveRegion="polite" style={styles.notice}>
          {notice}
        </Text>
      ) : null}
      {field('code', guardianPhoneTexts.code, { autoComplete: 'organization', textContentType: 'organizationName' })}
      {field('phone', guardianPhoneTexts.phone, {
        keyboardType: 'phone-pad',
        autoComplete: 'tel',
        textContentType: 'telephoneNumber',
        onSubmitEditing: () => void submit(),
      })}
      {error ? (
        <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
          {error}
        </Text>
      ) : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={sending ? guardianPhoneTexts.sending : guardianPhoneTexts.submit}
        accessibilityState={{ disabled: sending, busy: sending }}
        disabled={sending}
        onPress={() => void submit()}
        style={[styles.button, sending && styles.buttonDisabled]}
      >
        {sending ? (
          <ActivityIndicator color="#fff" accessibilityLabel={guardianPhoneTexts.sending} />
        ) : (
          <Text style={styles.buttonText}>{guardianPhoneTexts.submit}</Text>
        )}
      </Pressable>
    </ScrollView>
  );
}
