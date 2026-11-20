import type { GuardianConsentResult } from '@okul/api-client';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, Pressable, ScrollView, Switch, Text, View } from 'react-native';
import { formStyles as styles } from '../login/LoginScreen';

/** Spec 0007, User experience — KVKK (AC-4, AC-9). */
export const consentTexts = {
  title: 'KVKK Aydınlatması',
  agree: 'Verilerimin işlenmesine rıza veriyorum',
  submit: 'Devam et',
  hint: 'Lütfen rıza onayını işaretleyin.',
  loading: 'Yükleniyor…',
  network: 'Bağlantı kurulamadı, tekrar deneyin.',
} as const;

export interface ConsentScreenProps {
  load: () => Promise<GuardianConsentResult>;
  accept: () => Promise<GuardianConsentResult>;
  onAccepted: () => void;
  onSessionEnded: () => void;
}

export function ConsentScreen({ load, accept, onAccepted, onSessionEnded }: ConsentScreenProps) {
  const [text, setText] = useState<string | null>(null);
  const [agreed, setAgreed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    let active = true;
    void load().then((result) => {
      if (!active) return;
      if (result.kind === 'ok') setText(result.consent.text);
      else if (result.kind === 'unauthenticated') onSessionEnded();
      else setError(consentTexts.network);
    });
    return () => {
      active = false;
    };
  }, [load, onSessionEnded]);

  useEffect(() => {
    if (error) AccessibilityInfo.announceForAccessibility(error);
  }, [error]);

  async function submit() {
    if (!agreed || sending) return;
    setSending(true);
    setError(null);
    const result = await accept();
    setSending(false);
    if (result.kind === 'ok') onAccepted();
    else if (result.kind === 'unauthenticated') onSessionEnded();
    else setError(consentTexts.network);
  }

  const disabled = !agreed || sending || text === null;
  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text accessibilityRole="header" style={styles.title}>
        {consentTexts.title}
      </Text>
      <ScrollView style={{ maxHeight: 320, borderWidth: 1, borderColor: '#d1d5db', borderRadius: 8, padding: 12 }}>
        {text === null ? (
          <ActivityIndicator accessibilityLabel={consentTexts.loading} />
        ) : (
          <Text style={{ fontSize: 16, lineHeight: 24, color: '#111' }}>{text}</Text>
        )}
      </ScrollView>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 44 }}>
        <Switch
          accessibilityLabel={consentTexts.agree}
          accessibilityRole="checkbox"
          accessibilityState={{ checked: agreed }}
          value={agreed}
          onValueChange={setAgreed}
        />
        <Text style={{ flex: 1, fontSize: 16, color: '#111' }}>{consentTexts.agree}</Text>
      </View>
      {!agreed ? <Text style={{ color: '#92400e', fontSize: 16 }}>{consentTexts.hint}</Text> : null}
      {error ? (
        <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
          {error}
        </Text>
      ) : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={consentTexts.submit}
        accessibilityState={{ disabled }}
        disabled={disabled}
        onPress={() => void submit()}
        style={[styles.button, disabled && styles.buttonDisabled]}
      >
        <Text style={styles.buttonText}>{consentTexts.submit}</Text>
      </Pressable>
    </ScrollView>
  );
}
