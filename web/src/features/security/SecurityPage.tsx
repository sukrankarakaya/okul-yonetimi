import type { TwoFactorChangeResult, TwoFactorStatusResult } from '@okul/api-client';
import { useCallback, useEffect, useRef, useState } from 'react';
import { formStyles as styles } from '../login/LoginPage';
import { codeTexts, CodeForm, secondary, type CodeOutcome } from '../login/CodeForm';

/** Spec 0004, User experience — Güvenlik (Requirements 7, 8; AC-8, AC-9, AC-12, AC-13). */
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

export interface SecurityPageProps {
  loadStatus: () => Promise<TwoFactorStatusResult>;
  renewRecoveryCodes: (code: string) => Promise<TwoFactorChangeResult>;
  disableTwoFactor: (code: string) => Promise<TwoFactorChangeResult>;
  /** New codes → the Kurtarma kodları screen. */
  onRenewed: (codes: string[]) => void;
  onEnable: () => void;
  onSessionEnded: () => void;
  onBack: () => void;
  notice?: string;
}

export function SecurityPage(props: SecurityPageProps) {
  const { loadStatus, renewRecoveryCodes, disableTwoFactor, onRenewed, onEnable, onSessionEnded, onBack } = props;
  const [status, setStatus] = useState<TwoFactorStatusResult | null>(null);
  const [action, setAction] = useState<'renew' | 'disable' | null>(null);
  const [notice, setNotice] = useState(props.notice);

  const reload = useCallback(() => {
    void loadStatus().then((result) => (result.kind === 'unauthenticated' ? onSessionEnded() : setStatus(result)));
  }, [loadStatus, onSessionEnded]);
  useEffect(reload, [reload]);

  // The new codes of the last successful renewal, handed to the Kurtarma kodları screen.
  const made = useRef<string[] | undefined>(undefined);

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

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{securityTexts.title}</h1>
      {notice ? (
        <p role="status" style={styles.notice}>
          {notice}
        </p>
      ) : null}
      {status === null ? (
        <p role="status" aria-busy="true">
          Yükleniyor…
        </p>
      ) : status.kind !== 'ok' ? (
        <p role="alert" style={styles.error}>
          {codeTexts.network}
        </p>
      ) : (
        <>
          <p style={{ fontWeight: 600 }}>{status.status.enabled ? securityTexts.on : securityTexts.off}</p>
          {status.status.required ? <p>{securityTexts.mandatory}</p> : null}
          {status.status.enabled ? <p>{securityTexts.remaining(Number(status.status.remainingRecoveryCodes))}</p> : null}
          {status.status.enabled && Number(status.status.remainingRecoveryCodes) <= 3 ? (
            <p role="status" style={{ ...styles.notice, color: '#7c2d12', background: '#fff7ed' }}>
              {securityTexts.low}
            </p>
          ) : null}
          {action ? (
            <CodeForm
              submitLabel={action === 'renew' ? securityTexts.renew : securityTexts.disable}
              submit={async (code) => {
                const result = await (action === 'renew' ? renewRecoveryCodes(code) : disableTwoFactor(code));
                if (result.kind !== 'ok') {
                  return result;
                }
                made.current = result.recoveryCodes;
                return { kind: 'ok' };
              }}
              onDone={done}
              onCancel={() => setAction(null)}
            />
          ) : (
            <>
              {status.status.enabled ? (
                <button type="button" onClick={() => setAction('renew')} style={secondary}>
                  {securityTexts.renew}
                </button>
              ) : null}
              {status.status.canDisable ? (
                <button type="button" onClick={() => setAction('disable')} style={secondary}>
                  {securityTexts.disable}
                </button>
              ) : null}
              {!status.status.enabled && !status.status.required ? (
                <button type="button" onClick={onEnable} style={secondary}>
                  {securityTexts.enable}
                </button>
              ) : null}
            </>
          )}
        </>
      )}
      {action === null ? (
        <button type="button" onClick={onBack} style={secondary}>
          {codeTexts.cancel}
        </button>
      ) : null}
    </main>
  );
}
