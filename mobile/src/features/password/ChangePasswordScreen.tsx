import type { ChangePasswordResult } from '@okul/api-client';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { formStyles as styles } from '../login/LoginScreen';

/** Spec 0003, User experience — Şifre değiştir (same texts as the web panel). */
export const passwordTexts = {
  title: 'Şifre değiştir',
  current: 'Mevcut şifre',
  next: 'Yeni şifre',
  nextAgain: 'Yeni şifre (tekrar)',
  submit: 'Şifreyi değiştir',
  cancel: 'Vazgeç',
  tooShort: 'Yeni şifre en az 10 karakter olmalı.',
  mismatch: 'Yeni şifreler eşleşmiyor.',
  currentWrong: 'Mevcut şifre hatalı.',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
  changed: 'Şifreniz değiştirildi.',
  loading: 'Yükleniyor…',
} as const;

type Field = 'current' | 'next' | 'nextAgain';

export interface ChangePasswordScreenProps {
  changePassword: (current: string, next: string, nextAgain: string) => Promise<ChangePasswordResult>;
  onChanged: () => void;
  onCancel: () => void;
  onSessionEnded: () => void;
}

export function ChangePasswordScreen({ changePassword, onChanged, onCancel, onSessionEnded }: ChangePasswordScreenProps) {
  const [values, setValues] = useState<Record<Field, string>>({ current: '', next: '', nextAgain: '' });
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    if (error) {
      AccessibilityInfo.announceForAccessibility(error);
    }
  }, [error]);

  async function submit() {
    if (sending) {
      return;
    }
    setSending(true);
    setError(null);
    const result = await changePassword(values.current, values.next, values.nextAgain);
    setSending(false);
    const messages: Partial<Record<ChangePasswordResult, string>> = {
      tooShort: passwordTexts.tooShort,
      mismatch: passwordTexts.mismatch,
      currentWrong: passwordTexts.currentWrong,
      network: passwordTexts.network,
    };
    if (result === 'changed') {
      onChanged();
    } else if (result === 'unauthenticated') {
      onSessionEnded();
    } else {
      setError(messages[result] ?? passwordTexts.network);
    }
  }

  const field = (name: Field, label: string) => (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        accessibilityLabel={label}
        value={values[name]}
        onChangeText={(text) => setValues((v) => ({ ...v, [name]: text }))}
        secureTextEntry
        autoCapitalize="none"
        autoComplete={name === 'current' ? 'current-password' : 'new-password'}
        textContentType={name === 'current' ? 'password' : 'newPassword'}
        style={styles.input}
      />
    </View>
  );

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text accessibilityRole="header" style={styles.title}>
        {passwordTexts.title}
      </Text>
      {field('current', passwordTexts.current)}
      {field('next', passwordTexts.next)}
      {field('nextAgain', passwordTexts.nextAgain)}
      {error ? (
        <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
          {error}
        </Text>
      ) : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={passwordTexts.submit}
        accessibilityState={{ disabled: sending, busy: sending }}
        disabled={sending}
        onPress={() => void submit()}
        style={[styles.button, sending && styles.buttonDisabled]}
      >
        {sending ? <ActivityIndicator color="#fff" accessibilityLabel={passwordTexts.loading} /> : <Text style={styles.buttonText}>{passwordTexts.submit}</Text>}
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={passwordTexts.cancel} onPress={onCancel} style={styles.secondary}>
        <Text style={styles.secondaryText}>{passwordTexts.cancel}</Text>
      </Pressable>
    </ScrollView>
  );
}
