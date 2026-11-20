import * as Clipboard from 'expo-clipboard';
import { useEffect } from 'react';
import { AccessibilityInfo, Pressable, ScrollView, Text, View } from 'react-native';
import { formStyles as styles } from './LoginScreen';

/** Spec 0004, User experience — Kurtarma kodları, shown once (mobile: copy; the web also downloads). */
export const recoveryTexts = {
  title: 'Kurtarma kodlarınız',
  warning: 'Bu kodlar bir daha gösterilmeyecek. Güvenli bir yere kaydedin.',
  copy: 'Kopyala',
  continue: 'Kaydettim, devam et',
} as const;

export interface RecoveryCodesStepProps {
  codes: string[];
  notice?: string;
  onContinue: () => void;
}

export function RecoveryCodesStep({ codes, notice, onContinue }: RecoveryCodesStepProps) {
  useEffect(() => {
    AccessibilityInfo.announceForAccessibility(recoveryTexts.warning);
  }, []);

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text accessibilityRole="header" style={styles.title}>
        {recoveryTexts.title}
      </Text>
      {notice ? (
        <Text accessibilityLiveRegion="polite" style={styles.notice}>
          {notice}
        </Text>
      ) : null}
      <Text role="alert" accessibilityLiveRegion="polite" style={[styles.notice, { color: '#7c2d12', backgroundColor: '#fff7ed' }]}>
        {recoveryTexts.warning}
      </Text>
      <View accessibilityRole="list" style={{ flexDirection: 'row', flexWrap: 'wrap', rowGap: 8 }}>
        {codes.map((code) => (
          <Text key={code} selectable style={{ width: '50%', fontSize: 16, fontFamily: 'Menlo', color: '#111' }}>
            {code}
          </Text>
        ))}
      </View>
      <Pressable accessibilityRole="button" accessibilityLabel={recoveryTexts.copy} onPress={() => void Clipboard.setStringAsync(codes.join('\n'))} style={styles.secondary}>
        <Text style={styles.secondaryText}>{recoveryTexts.copy}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={recoveryTexts.continue} onPress={onContinue} style={styles.button}>
        <Text style={styles.buttonText}>{recoveryTexts.continue}</Text>
      </Pressable>
    </ScrollView>
  );
}
