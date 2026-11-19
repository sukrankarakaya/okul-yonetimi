import type { InstitutionResult } from '@okul/api-client';
import { useCallback, useEffect, useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

/** Spec 0001, User experience — the texts are part of the acceptance criteria. */
export const texts = {
  loading: 'Yükleniyor…',
  empty: 'Henüz şube tanımlanmamış.',
  error: 'Bilgiler alınamadı, tekrar deneyin.',
  retry: 'Tekrar dene',
  unauthenticated: 'Oturum açmanız gerekiyor.',
  campusList: 'Şubeler',
} as const;

type ScreenState = { kind: 'loading' } | InstitutionResult;

/**
 * The status text a screen reader must hear for a state (spec 0001, Accessibility), or null.
 * `accessibilityLiveRegion` covers Android only; iOS VoiceOver needs an explicit announcement.
 */
export function announcementFor(state: ScreenState): string | null {
  switch (state.kind) {
    case 'loading':
      return texts.loading;
    case 'unauthenticated':
      return texts.unauthenticated;
    case 'error':
      return texts.error;
    case 'ok':
      return state.institution.campuses.length === 0 ? `${state.institution.name}. ${texts.empty}` : null;
  }
}

export interface InstitutionScreenProps {
  loadInstitution: () => Promise<InstitutionResult>;
}

export function InstitutionScreen({ loadInstitution }: InstitutionScreenProps) {
  const [state, setState] = useState<ScreenState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    void loadInstitution().then((result) => {
      if (active) {
        setState(result);
      }
    });
    return () => {
      active = false;
    };
  }, [loadInstitution, attempt]);

  useEffect(() => {
    const message = announcementFor(state);
    if (message !== null) {
      AccessibilityInfo.announceForAccessibility(message);
    }
  }, [state]);

  const retry = useCallback(() => {
    setState({ kind: 'loading' });
    setAttempt((n) => n + 1);
  }, []);

  switch (state.kind) {
    case 'loading':
      return (
        <View style={styles.centered}>
          <ActivityIndicator accessibilityLabel={texts.loading} />
          <Text accessibilityLiveRegion="polite">{texts.loading}</Text>
        </View>
      );

    case 'unauthenticated':
      return (
        <View style={styles.centered}>
          <Text accessibilityLiveRegion="polite" style={styles.message}>
            {texts.unauthenticated}
          </Text>
        </View>
      );

    case 'error':
      return (
        <View style={styles.centered}>
          <Text role="alert" accessibilityLiveRegion="assertive" style={styles.message}>
            {texts.error}
          </Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={texts.retry}
            onPress={retry}
            style={styles.button}
          >
            <Text style={styles.buttonText}>{texts.retry}</Text>
          </Pressable>
        </View>
      );

    case 'ok': {
      const { institution } = state;
      return (
        <ScrollView contentContainerStyle={styles.content}>
          {/* Names are rendered as plain text — never interpreted as markup (spec 0001, G6). */}
          <Text accessibilityRole="header" style={styles.title}>
            {institution.name}
          </Text>
          {institution.campuses.length === 0 ? (
            <Text accessibilityLiveRegion="polite" style={styles.message}>
              {texts.empty}
            </Text>
          ) : (
            <View role="list" accessibilityLabel={texts.campusList}>
              {institution.campuses.map((campus) => (
                <Text key={campus.id} role="listitem" style={styles.campus}>
                  {campus.name}
                </Text>
              ))}
            </View>
          )}
        </ScrollView>
      );
    }
  }
}

const styles = StyleSheet.create({
  centered: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24, gap: 12 },
  content: { padding: 24, gap: 12 },
  title: { fontSize: 24, fontWeight: '600', color: '#111' },
  message: { fontSize: 16, color: '#222', textAlign: 'center' },
  campus: { fontSize: 16, color: '#222', paddingVertical: 8 },
  button: { backgroundColor: '#1d4ed8', paddingHorizontal: 20, paddingVertical: 12, borderRadius: 8 },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
});
