import * as Clipboard from 'expo-clipboard';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Image, Pressable, ScrollView, Text, View } from 'react-native';
import { formStyles as styles } from './LoginScreen';
import { codeTexts, CodeForm, type CodeOutcome } from './CodeForm';

/** Spec 0004, User experience — Kurulum (same texts as the web panel). */
export const setupTexts = {
  title: 'İki adımlı doğrulamayı kurun',
  hint: 'Doğrulama uygulamanızla QR kodunu okutun veya anahtarı elle girin.',
  key: 'Anahtar',
  copy: 'Kopyala',
  qr: 'Doğrulama uygulaması için QR kodu',
  submit: 'Kurulumu tamamla',
} as const;

export type SetupLoad = { kind: 'ok'; key: string; qrCode: string } | { kind: 'stepEnded' } | { kind: 'network' } | { kind: 'unauthenticated' };

export type SetupConfirm = { kind: 'ok'; recoveryCodes: string[] } | Exclude<CodeOutcome, { kind: 'ok' }>;

export interface SetupStepProps {
  load: () => Promise<SetupLoad>;
  confirm: (code: string) => Promise<SetupConfirm>;
  onConfirmed: (recoveryCodes: string[]) => void;
  onEnded: (outcome: CodeOutcome) => void;
  onCancel: () => void;
}

export function SetupStep({ load, confirm, onConfirmed, onEnded, onCancel }: SetupStepProps) {
  const [setup, setSetup] = useState<SetupLoad | null>(null);
  const codes = useRef<string[]>([]);

  useEffect(() => {
    let active = true;
    void load().then((result) => {
      if (!active) return;
      if (result.kind === 'stepEnded' || result.kind === 'unauthenticated') onEnded(result);
      else setSetup(result);
    });
    return () => {
      active = false;
    };
  }, [load, onEnded]);

  const submit = async (code: string): Promise<CodeOutcome> => {
    const result = await confirm(code);
    if (result.kind !== 'ok') return result;
    codes.current = result.recoveryCodes;
    return { kind: 'ok' };
  };

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text accessibilityRole="header" style={styles.title}>
        {setupTexts.title}
      </Text>
      {setup === null ? (
        <ActivityIndicator accessibilityLabel={codeTexts.loading} />
      ) : setup.kind === 'ok' ? (
        <CodeForm
          submitLabel={setupTexts.submit}
          submit={submit}
          onDone={(outcome) => (outcome.kind === 'ok' ? onConfirmed(codes.current) : onEnded(outcome))}
          onCancel={onCancel}
        >
          <Text style={styles.label}>{setupTexts.hint}</Text>
          <Image source={{ uri: setup.qrCode }} accessibilityLabel={setupTexts.qr} accessible style={{ width: 180, height: 180, alignSelf: 'center' }} />
          <View style={{ gap: 6 }}>
            <Text style={styles.label}>{setupTexts.key}</Text>
            <Text selectable style={{ fontSize: 16, letterSpacing: 1, color: '#111' }}>
              {setup.key}
            </Text>
            <Pressable accessibilityRole="button" accessibilityLabel={setupTexts.copy} onPress={() => void Clipboard.setStringAsync(setup.key)} style={styles.secondary}>
              <Text style={styles.secondaryText}>{setupTexts.copy}</Text>
            </Pressable>
          </View>
        </CodeForm>
      ) : (
        <>
          <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
            {codeTexts.network}
          </Text>
          <Pressable accessibilityRole="button" accessibilityLabel={codeTexts.cancel} onPress={onCancel} style={styles.secondary}>
            <Text style={styles.secondaryText}>{codeTexts.cancel}</Text>
          </Pressable>
        </>
      )}
    </ScrollView>
  );
}
