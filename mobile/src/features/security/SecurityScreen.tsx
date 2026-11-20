import type { TwoFactorChangeResult, TwoFactorStatusResult } from '@okul/api-client';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, Text } from 'react-native';
import { formStyles as styles } from '../login/LoginScreen';
import { codeTexts, CodeForm, type CodeOutcome } from '../login/CodeForm';

/** Spec 0004, User experience — Güvenlik (same texts as the web panel). */
export const securityTexts = {
  title: 'Güvenlik',
  on: 'İki adımlı doğrulama: Açık',
  off: 'İki adımlı doğrulama: Kapalı',
  mandatory: 'Rolünüz gereği zorunlu.',
  remaining: (n: number) => `Kalan kurtarma kodu: ${n}`,
  low: 'Kurtarma kodlarınız azaldı. Yenilemeniz önerilir.',
  renew: 'Kurtarma kodlarını yenile',
  disable: 'İki adımlı doğrulamayı kapat',
  enable: 'İki adımlı doğrulamayı aç',
  renewed: 'Yeni kurtarma kodlarınız oluşturuldu.',
  disabled: 'İki adımlı doğrulama kapatıldı.',
} as const;

export interface SecurityScreenProps {
  loadStatus: () => Promise<TwoFactorStatusResult>;
  renewRecoveryCodes: (code: string) => Promise<TwoFactorChangeResult>;
  disableTwoFactor: (code: string) => Promise<TwoFactorChangeResult>;
  onRenewed: (codes: string[]) => void;
  onEnable: () => void;
  onSessionEnded: () => void;
  onBack: () => void;
}

export function SecurityScreen({ loadStatus, renewRecoveryCodes, disableTwoFactor, onRenewed, onEnable, onSessionEnded, onBack }: SecurityScreenProps) {
  const [status, setStatus] = useState<TwoFactorStatusResult | null>(null);
  const [action, setAction] = useState<'renew' | 'disable' | null>(null);
  const [notice, setNotice] = useState<string | undefined>();
  const made = useRef<string[] | undefined>(undefined);

  const reload = useCallback(() => {
    void loadStatus().then((result) => (result.kind === 'unauthenticated' ? onSessionEnded() : setStatus(result)));
  }, [loadStatus, onSessionEnded]);
  useEffect(reload, [reload]);

  const done = (outcome: CodeOutcome) => {
    if (outcome.kind === 'unauthenticated') {
      onSessionEnded();
    } else if (outcome.kind === 'ok' && made.current) {
      onRenewed(made.current);
    } else {
      setAction(null);
      setNotice(outcome.kind === 'ok' ? securityTexts.disabled : outcome.kind === 'notAllowed' ? securityTexts.mandatory : codeTexts.network);
      reload();
    }
  };

  const button = (label: string, onPress: () => void) => (
    <Pressable key={label} accessibilityRole="button" accessibilityLabel={label} onPress={onPress} style={styles.secondary}>
      <Text style={styles.secondaryText}>{label}</Text>
    </Pressable>
  );

  const s = status?.kind === 'ok' ? status.status : null;
  const left = s ? Number(s.remainingRecoveryCodes) : 0;
  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text accessibilityRole="header" style={styles.title}>
        {securityTexts.title}
      </Text>
      {notice ? (
        <Text accessibilityLiveRegion="polite" style={styles.notice}>
          {notice}
        </Text>
      ) : null}
      {status === null ? <ActivityIndicator accessibilityLabel={codeTexts.loading} /> : null}
      {status !== null && !s ? (
        <Text role="alert" accessibilityLiveRegion="assertive" style={styles.error}>
          {codeTexts.network}
        </Text>
      ) : null}
      {s ? (
        <>
          <Text style={[styles.label, { fontWeight: '600' }]}>{s.enabled ? securityTexts.on : securityTexts.off}</Text>
          {s.required ? <Text style={styles.label}>{securityTexts.mandatory}</Text> : null}
          {s.enabled ? <Text style={styles.label}>{securityTexts.remaining(left)}</Text> : null}
          {s.enabled && left <= 3 ? (
            <Text accessibilityLiveRegion="polite" style={[styles.notice, { color: '#7c2d12', backgroundColor: '#fff7ed' }]}>
              {securityTexts.low}
            </Text>
          ) : null}
          {action ? (
            <CodeForm
              submitLabel={action === 'renew' ? securityTexts.renew : securityTexts.disable}
              submit={async (code) => {
                const result = await (action === 'renew' ? renewRecoveryCodes(code) : disableTwoFactor(code));
                if (result.kind !== 'ok') return result;
                made.current = result.recoveryCodes;
                return { kind: 'ok' };
              }}
              onDone={done}
              onCancel={() => setAction(null)}
            />
          ) : (
            <>
              {s.enabled ? button(securityTexts.renew, () => setAction('renew')) : null}
              {s.canDisable ? button(securityTexts.disable, () => setAction('disable')) : null}
              {!s.enabled && !s.required ? button(securityTexts.enable, onEnable) : null}
            </>
          )}
        </>
      ) : null}
      {action === null ? button(codeTexts.cancel, onBack) : null}
    </ScrollView>
  );
}
