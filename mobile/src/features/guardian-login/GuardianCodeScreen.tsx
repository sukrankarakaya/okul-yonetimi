import type { GuardianCodeResult, GuardianVerifyResult } from '@okul/api-client';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { formStyles as styles } from '../login/LoginScreen';
import { guardianPhoneTexts } from './GuardianPhoneScreen';

/** Spec 0007, User experience — SMS kodu (AC-2, AC-3, AC-9; G-1, G-2). */
export const guardianCodeTexts = {
  title: 'SMS Kodunu Girin',
  sent: 'Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.',
  code: '6 haneli kod',
  submit: 'Doğrula',
  verifying: 'Doğrulanıyor...',
  resend: 'SMS kodunu tekrar gönder',
  cancel: 'Vazgeç',
  required: 'Bu alan gerekli.',
  invalidCode: 'Kod hatalı veya süresi geçmiş.',
  expired: 'Kodun süresi doldu. Yeni kod isteyin.',
  locked: 'Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
} as const;

export interface GuardianCodeScreenProps {
  verify: (code: string) => Promise<GuardianVerifyResult>;
  resend: () => Promise<GuardianCodeResult>;
  onCancel: () => void;
  onLoggedIn: (consentRequired: boolean) => void;
}

export function GuardianCodeScreen({ verify, resend, onCancel, onLoggedIn }: GuardianCodeScreenProps) {
  const [code, setCode] = useState('');
  const [missing, setMissing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(guardianCodeTexts.sent);
  const [sending, setSending] = useState(false);
  const [resendBlocked, setResendBlocked] = useState(false);

  useEffect(() => {
    const message = error ?? (missing ? guardianCodeTexts.required : notice);
    if (message) AccessibilityInfo.announceForAccessibility(message);
  }, [error, missing, notice]);

  async function submit() {
    if (sending) return;
    setMissing(code.trim() === '');
    if (code.trim() === '') return;

    setSending(true);
    setError(null);
    const result = await verify(code);
    setSending(false);
    if (result.kind === 'ok') {
      onLoggedIn(result.consentRequired);
      return;
    }
    setCode('');
    setError(
      result.kind === 'invalidCode'
        ? guardianCodeTexts.invalidCode
        : result.kind === 'expired'
          ? guardianCodeTexts.expired
          : result.kind === 'locked'
            ? guardianCodeTexts.locked
            : guardianCodeTexts.network,
    );
  }

  async function again() {
    setError(null);
    const result = await resend();
    if (result.kind === 'sent') {
      setNotice(guardianCodeTexts.sent);
      return;
    }
    setNotice(null);
    if (result.kind === 'rateLimited') {
      setResendBlocked(true);
      setError(guardianPhoneTexts.rateLimited);
      return;
    }
    setError(result.kind === 'smsFailed' ? guardianPhoneTexts.smsFailed : guardianCodeTexts.network);
  }

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      {notice ? (
        <Text accessibilityLiveRegion="polite" style={styles.notice}>
          {notice}
        </Text>
      ) : null}
      <Text accessibilityRole="header" style={styles.title}>
        {guardianCodeTexts.title}
      </Text>
      <View style={styles.field}>
        <Text nativeID="guardian-code" style={styles.label}>
          {guardianCodeTexts.code}
        </Text>
        <TextInput
          accessibilityLabel={guardianCodeTexts.code}
          accessibilityLabelledBy="guardian-code"
          value={code}
          onChangeText={setCode}
          keyboardType="number-pad"
          autoComplete="one-time-code"
          textContentType="oneTimeCode"
          onSubmitEditing={() => void submit()}
          style={styles.input}
        />
        {missing ? (
          <Text accessibilityLiveRegion="polite" style={styles.fieldError}>
            {guardianCodeTexts.required}
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
        accessibilityLabel={sending ? guardianCodeTexts.verifying : guardianCodeTexts.submit}
        accessibilityState={{ disabled: sending, busy: sending }}
        disabled={sending}
        onPress={() => void submit()}
        style={[styles.button, sending && styles.buttonDisabled]}
      >
        {sending ? (
          <ActivityIndicator color="#fff" accessibilityLabel={guardianCodeTexts.verifying} />
        ) : (
          <Text style={styles.buttonText}>{guardianCodeTexts.submit}</Text>
        )}
      </Pressable>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={guardianCodeTexts.resend}
        accessibilityState={{ disabled: resendBlocked }}
        disabled={resendBlocked}
        onPress={() => void again()}
        style={[styles.secondary, resendBlocked && styles.buttonDisabled]}
      >
        <Text style={styles.secondaryText}>{guardianCodeTexts.resend}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={guardianCodeTexts.cancel} onPress={onCancel} style={styles.secondary}>
        <Text style={styles.secondaryText}>{guardianCodeTexts.cancel}</Text>
      </Pressable>
    </ScrollView>
  );
}
