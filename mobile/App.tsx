import { createOkulClient, type OkulClient } from '@okul/api-client';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, SafeAreaView, StyleSheet } from 'react-native';
import { readLoginHint, secureTokenStore, writeLoginHint, type LoginHint } from './src/auth/secureSession';
import { ConsentScreen } from './src/features/guardian-login/ConsentScreen';
import { GuardianCodeScreen } from './src/features/guardian-login/GuardianCodeScreen';
import { GuardianPhoneScreen } from './src/features/guardian-login/GuardianPhoneScreen';
import { InstitutionScreen } from './src/features/institution/InstitutionScreen';
import { codeTexts, type CodeOutcome } from './src/features/login/CodeForm';
import { LoginScreen, loginTexts } from './src/features/login/LoginScreen';
import { RecoveryCodesStep } from './src/features/login/RecoveryCodesStep';
import { SetupStep } from './src/features/login/SetupStep';
import { VerifyStep } from './src/features/login/VerifyStep';
import { ChangePasswordScreen, passwordTexts } from './src/features/password/ChangePasswordScreen';
import { SecurityScreen, securityTexts } from './src/features/security/SecurityScreen';

const baseUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';

/**
 * Mobile app (spec 0003). Tokens live in the device's secure store (Requirement 8); there is no automatic test login
 * any more (Requirement 9) — without a session the app opens on Giriş, with kurum kodu and e-posta remembered.
 */
export function createMobileClient(): OkulClient {
  return createOkulClient({ baseUrl, kind: 'mobile', tokenStore: secureTokenStore() });
}

type Screen =
  | { kind: 'checking' }
  | { kind: 'login'; notice?: string; hint: LoginHint | null }
  // Spec 0004 — the step after a right password, and the Güvenlik screens.
  | { kind: 'verify'; step: string }
  | { kind: 'setup'; step: string }
  | { kind: 'enable' }
  | { kind: 'recovery'; codes: string[]; next: 'institution' | 'security'; notice?: string }
  | { kind: 'security' }
  | { kind: 'institution'; notice?: string }
  | { kind: 'password' }
  // Spec 0007 — veli girişi; the kurum kodu is remembered, the telefon never (G-5).
  | { kind: 'guardianPhone'; notice?: string; code?: string; phone?: string }
  | { kind: 'guardianCode'; code: string; phone: string }
  | { kind: 'consent' };

export interface AppProps {
  client?: OkulClient;
  loginHint?: { read: () => Promise<LoginHint | null>; write: (hint: LoginHint) => Promise<void> };
}

export default function App({ client: injected, loginHint }: AppProps = {}) {
  const client = useMemo(() => injected ?? createMobileClient(), [injected]);
  const hints = useMemo(() => loginHint ?? { read: () => readLoginHint(), write: (h: LoginHint) => writeLoginHint(h) }, [loginHint]);
  const [screen, setScreen] = useState<Screen>({ kind: 'checking' });
  const [guardian, setGuardian] = useState(false);

  const toLogin = useCallback(
    async (notice?: string) => setScreen({ kind: 'login', notice, hint: await hints.read() }),
    [hints],
  );

  const toGuardianLogin = useCallback(
    async (notice?: string) => setScreen({ kind: 'guardianPhone', notice, code: (await hints.read())?.institutionCode }),
    [hints],
  );

  useEffect(() => {
    let active = true;
    void client.hasSession().then(async (has) => {
      if (!active) return;
      if (!has) {
        await toLogin();
        return;
      }
      // A veli without the current rıza sees KVKK first (spec 0007, AC-4); a staff session is "notGuardian".
      const consent = await client.getGuardianConsent();
      if (!active) return;
      const isGuardian = consent.kind === 'ok';
      setGuardian(isGuardian);
      setScreen(isGuardian && !consent.consent.accepted ? { kind: 'consent' } : { kind: 'institution' });
    });
    return () => {
      active = false;
    };
  }, [client, toLogin]);

  const loadInstitution = useCallback(() => client.getMyInstitution(), [client]);
  const sessionEnded = useCallback(
    (expired: boolean) => void (guardian ? toGuardianLogin : toLogin)(expired ? loginTexts.expired : undefined),
    [guardian, toGuardianLogin, toLogin],
  );
  const loadConsent = useCallback(() => client.getGuardianConsent(), [client]);
  const consentEnded = useCallback(() => sessionEnded(true), [sessionEnded]);
  const stepEnded = useCallback(() => void toLogin(codeTexts.stepEnded), [toLogin]);
  const loadStatus = useCallback(() => client.getTwoFactor(), [client]);
  const setupStep = screen.kind === 'setup' ? screen.step : null;
  const loadSetup = useCallback(() => client.startSetup(setupStep ?? ''), [client, setupStep]);
  const enableStep = useRef<string>('');
  const loadEnable = useCallback(async () => {
    const result = await client.startEnableTwoFactor();
    if (result.kind === 'ok') {
      enableStep.current = result.step;
    }
    // Already on: nothing to set up — back to Güvenlik, which shows "Açık" (review round 1 I-1).
    return result.kind === 'alreadyEnabled' ? ({ kind: 'stepEnded' } as const) : result;
  }, [client]);
  const toSecurity = useCallback(() => setScreen({ kind: 'security' }), []);
  const enableEnded = useCallback(
    (outcome: CodeOutcome) => (outcome.kind === 'unauthenticated' ? sessionEnded(true) : toSecurity()),
    [sessionEnded, toSecurity],
  );
  const cancelStep = (step: string) => {
    void client.cancelLogin(step);
    void toLogin();
  };

  return (
    <SafeAreaView style={styles.root}>
      {screen.kind === 'checking' ? <ActivityIndicator accessibilityLabel={loginTexts.loading} style={styles.root} /> : null}
      {screen.kind === 'login' ? (
        <LoginScreen
          login={(code, email, password) => client.login(code, email, password)}
          onLoggedIn={(institutionCode, email) => {
            void hints.write({ institutionCode, email });
            setScreen({ kind: 'institution' });
          }}
          onStep={(next, step, institutionCode, email) => {
            void hints.write({ institutionCode, email });
            setScreen({ kind: next, step });
          }}
          notice={screen.notice}
          initialCode={screen.hint?.institutionCode}
          initialEmail={screen.hint?.email}
          onGuardian={() => void toGuardianLogin()}
        />
      ) : null}
      {screen.kind === 'guardianPhone' ? (
        <GuardianPhoneScreen
          request={(code, phone) => client.requestGuardianCode(code, phone)}
          onSent={(code, phone) => {
            void hints.read().then((hint) => hints.write({ institutionCode: code, email: hint?.email ?? '' }));
            setScreen({ kind: 'guardianCode', code, phone });
          }}
          notice={screen.notice}
          initialCode={screen.code}
          initialPhone={screen.phone}
        />
      ) : null}
      {screen.kind === 'guardianCode' ? (
        <GuardianCodeScreen
          verify={(code) => client.verifyGuardianCode(screen.code, screen.phone, code)}
          resend={() => client.requestGuardianCode(screen.code, screen.phone)}
          onCancel={() => {
            void client.cancelGuardianCode(screen.code, screen.phone);
            setScreen({ kind: 'guardianPhone', code: screen.code, phone: screen.phone });
          }}
          onLoggedIn={(consentRequired) => {
            setGuardian(true);
            setScreen(consentRequired ? { kind: 'consent' } : { kind: 'institution' });
          }}
        />
      ) : null}
      {screen.kind === 'consent' ? (
        <ConsentScreen
          load={loadConsent}
          accept={() => client.acceptGuardianConsent()}
          onAccepted={() => setScreen({ kind: 'institution' })}
          onSessionEnded={consentEnded}
        />
      ) : null}
      {screen.kind === 'verify' ? (
        <VerifyStep
          verify={(code) => client.verifyLogin(screen.step, code)}
          verifyWithRecoveryCode={(code) => client.verifyWithRecoveryCode(screen.step, code)}
          onVerified={() => setScreen({ kind: 'institution' })}
          onStepEnded={stepEnded}
          onCancel={() => cancelStep(screen.step)}
        />
      ) : null}
      {screen.kind === 'setup' ? (
        <SetupStep
          load={loadSetup}
          confirm={(code) => client.confirmSetup(screen.step, code)}
          onConfirmed={(codes) => setScreen({ kind: 'recovery', codes, next: 'institution' })}
          onEnded={stepEnded}
          onCancel={() => cancelStep(screen.step)}
        />
      ) : null}
      {screen.kind === 'enable' ? (
        <SetupStep
          load={loadEnable}
          confirm={async (code) => {
            const result = await client.confirmEnableTwoFactor(enableStep.current, code);
            return result.kind === 'ok' ? { kind: 'ok', recoveryCodes: result.recoveryCodes ?? [] } : result;
          }}
          onConfirmed={(codes) => setScreen({ kind: 'recovery', codes, next: 'security' })}
          onEnded={enableEnded}
          onCancel={toSecurity}
        />
      ) : null}
      {screen.kind === 'recovery' ? (
        <RecoveryCodesStep
          codes={screen.codes}
          notice={screen.notice}
          onContinue={() => setScreen(screen.next === 'security' ? { kind: 'security' } : { kind: 'institution' })}
        />
      ) : null}
      {screen.kind === 'security' ? (
        <SecurityScreen
          loadStatus={loadStatus}
          renewRecoveryCodes={(code) => client.renewRecoveryCodes(code)}
          disableTwoFactor={(code) => client.disableTwoFactor(code)}
          onRenewed={(codes) => setScreen({ kind: 'recovery', codes, next: 'security', notice: securityTexts.renewed })}
          onEnable={() => setScreen({ kind: 'enable' })}
          onSessionEnded={() => sessionEnded(true)}
          onBack={() => setScreen({ kind: 'institution' })}
        />
      ) : null}
      {screen.kind === 'password' ? (
        <ChangePasswordScreen
          changePassword={(current, next, again) => client.changePassword(current, next, again)}
          onChanged={() => setScreen({ kind: 'institution', notice: passwordTexts.changed })}
          onCancel={() => setScreen({ kind: 'institution' })}
          onSessionEnded={() => sessionEnded(true)}
        />
      ) : null}
      {screen.kind === 'institution' ? (
        <InstitutionScreen
          loadInstitution={loadInstitution}
          notice={screen.notice}
          onSessionEnded={sessionEnded}
          onChangePassword={guardian ? undefined : () => setScreen({ kind: 'password' })}
          onSecurity={guardian ? undefined : () => setScreen({ kind: 'security' })}
          onLogout={() => void client.logout().finally(() => void (guardian ? toGuardianLogin() : toLogin()))}
        />
      ) : null}
      <StatusBar style="dark" />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#fff' },
});
