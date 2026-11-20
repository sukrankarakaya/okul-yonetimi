import { createOkulClient, type OkulClient } from '@okul/api-client';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ConsentPage } from './features/guardian-login/ConsentPage';
import { GuardianCodePage } from './features/guardian-login/GuardianCodePage';
import { GuardianPhonePage } from './features/guardian-login/GuardianPhonePage';
import { InstitutionPage } from './features/institution/InstitutionPage';
import type { CodeOutcome } from './features/login/CodeForm';
import { codeTexts } from './features/login/CodeForm';
import { LoginPage, loginTexts } from './features/login/LoginPage';
import { RecoveryCodesStep } from './features/login/RecoveryCodesStep';
import { SetupStep } from './features/login/SetupStep';
import { VerifyStep } from './features/login/VerifyStep';
import { ChangePasswordPage, passwordTexts } from './features/password/ChangePasswordPage';
import { SecurityPage, securityTexts } from './features/security/SecurityPage';

const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

/**
 * Web panel (spec 0003). The session lives in httpOnly cookies the page cannot read — nothing in localStorage or
 * sessionStorage (Requirement 8). Tabs share it; a refresh is serialized across tabs with Web Locks (plan 0003, R5).
 * There is no automatic test login any more (Requirement 9): the app opens on Giriş without a session.
 */
export function createWebClient(fetchImpl?: typeof fetch): OkulClient {
  return createOkulClient({
    baseUrl,
    kind: 'web',
    fetch: fetchImpl,
    lock: typeof navigator !== 'undefined' && navigator.locks
      ? <T,>(name: string, work: () => Promise<T>) => navigator.locks.request(name, work) as Promise<T>
      : undefined,
  });
}

type Screen =
  | { kind: 'checking' }
  | { kind: 'login'; notice?: string }
  // Spec 0004 — the step after a right password, and the Güvenlik screens.
  | { kind: 'verify'; step: string }
  | { kind: 'setup'; step: string }
  | { kind: 'enable' }
  | { kind: 'recovery'; codes: string[]; next: 'institution' | 'security'; notice?: string }
  | { kind: 'security'; notice?: string }
  | { kind: 'institution'; notice?: string }
  | { kind: 'password' }
  // Spec 0007 — veli girişi.
  | { kind: 'guardianPhone'; notice?: string; code?: string; phone?: string }
  | { kind: 'guardianCode'; code: string; phone: string }
  | { kind: 'consent' };

export function App({ client: injected }: { client?: OkulClient } = {}) {
  const client = useMemo(() => injected ?? createWebClient(), [injected]);
  const [screen, setScreen] = useState<Screen>({ kind: 'checking' });
  // Spec 0007: a veli session — no staff actions, and an ended session returns to Veli Girişi.
  const [guardian, setGuardian] = useState(false);

  useEffect(() => {
    let active = true;
    void (async () => {
      if (!(await client.hasSession())) {
        if (active) setScreen({ kind: 'login' });
        return;
      }
      // A veli without the current rıza sees the KVKK screen first (spec 0007, AC-4); a staff session is "notGuardian".
      const consent = await client.getGuardianConsent();
      if (!active) return;
      const isGuardian = consent.kind === 'ok';
      setGuardian(isGuardian);
      setScreen(isGuardian && !consent.consent.accepted ? { kind: 'consent' } : { kind: 'institution' });
    })();
    return () => {
      active = false;
    };
  }, [client]);

  const loadInstitution = useCallback(() => client.getMyInstitution(), [client]);
  const sessionEnded = useCallback(
    (expired: boolean) =>
      setScreen(
        guardian
          ? { kind: 'guardianPhone', notice: expired ? loginTexts.expired : undefined }
          : { kind: 'login', notice: expired ? loginTexts.expired : undefined },
      ),
    [guardian],
  );
  const loadConsent = useCallback(() => client.getGuardianConsent(), [client]);
  const consentEnded = useCallback(() => sessionEnded(true), [sessionEnded]);
  const stepEnded = useCallback(() => setScreen({ kind: 'login', notice: codeTexts.stepEnded }), []);
  const loadStatus = useCallback(() => client.getTwoFactor(), [client]);

  // Setup at login, or an Öğretmen turning it on from Güvenlik (the enable step comes with the load).
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

  switch (screen.kind) {
    case 'checking':
      return (
        <p role="status" aria-busy="true" style={{ fontFamily: 'system-ui, sans-serif', padding: 24 }}>
          Yükleniyor…
        </p>
      );
    case 'login':
      return (
        <LoginPage
          login={(code, email, password) => client.login(code, email, password)}
          onLoggedIn={() => setScreen({ kind: 'institution' })}
          onStep={(next, step) => setScreen({ kind: next, step })}
          notice={screen.notice}
          onGuardian={() => setScreen({ kind: 'guardianPhone' })}
        />
      );
    case 'guardianPhone':
      return (
        <GuardianPhonePage
          request={(code, phone) => client.requestGuardianCode(code, phone)}
          onSent={(code, phone) => setScreen({ kind: 'guardianCode', code, phone })}
          notice={screen.notice}
          initialCode={screen.code}
          initialPhone={screen.phone}
        />
      );
    case 'guardianCode':
      return (
        <GuardianCodePage
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
      );
    case 'consent':
      return (
        <ConsentPage
          load={loadConsent}
          accept={() => client.acceptGuardianConsent()}
          onAccepted={() => setScreen({ kind: 'institution' })}
          onSessionEnded={consentEnded}
        />
      );
    case 'verify':
      return (
        <VerifyStep
          verify={(code) => client.verifyLogin(screen.step, code)}
          verifyWithRecoveryCode={(code) => client.verifyWithRecoveryCode(screen.step, code)}
          onVerified={() => setScreen({ kind: 'institution' })}
          onStepEnded={stepEnded}
          onCancel={() => {
            void client.cancelLogin(screen.step);
            setScreen({ kind: 'login' });
          }}
        />
      );
    case 'setup':
      return (
        <SetupStep
          load={loadSetup}
          confirm={(code) => client.confirmSetup(screen.step, code)}
          onConfirmed={(codes) => setScreen({ kind: 'recovery', codes, next: 'institution' })}
          onEnded={stepEnded}
          onCancel={() => {
            void client.cancelLogin(screen.step);
            setScreen({ kind: 'login' });
          }}
        />
      );
    case 'enable':
      return (
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
      );
    case 'recovery':
      return (
        <RecoveryCodesStep
          codes={screen.codes}
          notice={screen.notice}
          onContinue={() => setScreen(screen.next === 'security' ? { kind: 'security' } : { kind: 'institution' })}
        />
      );
    case 'security':
      return (
        <SecurityPage
          loadStatus={loadStatus}
          renewRecoveryCodes={(code) => client.renewRecoveryCodes(code)}
          disableTwoFactor={(code) => client.disableTwoFactor(code)}
          onRenewed={(codes) => setScreen({ kind: 'recovery', codes, next: 'security', notice: securityTexts.renewed })}
          onEnable={() => setScreen({ kind: 'enable' })}
          onSessionEnded={() => sessionEnded(true)}
          onBack={() => setScreen({ kind: 'institution' })}
          notice={screen.notice}
        />
      );
    case 'password':
      return (
        <ChangePasswordPage
          changePassword={(current, next, again) => client.changePassword(current, next, again)}
          onChanged={() => setScreen({ kind: 'institution', notice: passwordTexts.changed })}
          onCancel={() => setScreen({ kind: 'institution' })}
          onSessionEnded={() => sessionEnded(true)}
        />
      );
    case 'institution':
      return (
        <InstitutionPage
          loadInstitution={loadInstitution}
          notice={screen.notice}
          onSessionEnded={sessionEnded}
          onChangePassword={guardian ? undefined : () => setScreen({ kind: 'password' })}
          onSecurity={guardian ? undefined : () => setScreen({ kind: 'security' })}
          onLogout={() => {
            void client.logout().finally(() => setScreen(guardian ? { kind: 'guardianPhone' } : { kind: 'login' }));
          }}
        />
      );
  }
}
