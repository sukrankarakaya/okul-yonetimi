import { formStyles as styles } from './LoginPage';
import { secondary } from './CodeForm';

/** Spec 0004, User experience — Kurtarma kodları (Requirement 3; AC-3, AC-13). Shown once. */
export const recoveryTexts = {
  title: 'Kurtarma kodlarınız',
  warning: 'Bu kodlar bir daha gösterilmeyecek. Güvenli bir yere kaydedin.',
  copy: 'Kopyala',
  download: 'İndir',
  continue: 'Kaydettim, devam et',
} as const;

export interface RecoveryCodesStepProps {
  codes: string[];
  /** Shown above the list, e.g. "Yeni kurtarma kodlarınız oluşturuldu." */
  notice?: string;
  onContinue: () => void;
}

export function RecoveryCodesStep({ codes, notice, onContinue }: RecoveryCodesStepProps) {
  const text = codes.join('\n');
  const download = `data:text/plain;charset=utf-8,${encodeURIComponent(text + '\n')}`;
  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{recoveryTexts.title}</h1>
      {notice ? (
        <p role="status" style={styles.notice}>
          {notice}
        </p>
      ) : null}
      <p role="alert" style={{ ...styles.notice, color: '#7c2d12', background: '#fff7ed' }}>
        {recoveryTexts.warning}
      </p>
      <ol style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px 16px', fontFamily: 'ui-monospace, monospace', fontSize: 16 }}>
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ol>
      <div style={{ display: 'flex', gap: 12 }}>
        <button type="button" onClick={() => void navigator.clipboard?.writeText(text)} style={{ ...secondary, marginTop: 0 }}>
          {recoveryTexts.copy}
        </button>
        <a href={download} download="okul-kurtarma-kodlari.txt" style={{ ...secondary, marginTop: 0, textAlign: 'center', textDecoration: 'none', boxSizing: 'border-box' }}>
          {recoveryTexts.download}
        </a>
      </div>
      <button type="button" onClick={onContinue} style={{ ...styles.button, marginTop: 16 }}>
        {recoveryTexts.continue}
      </button>
    </main>
  );
}
