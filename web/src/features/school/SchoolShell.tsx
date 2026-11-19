import type { InstitutionResult, OkulClient } from '@okul/api-client';
import { useCallback, useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { AnnouncementsPage } from './AnnouncementsPage';
import { AttendancePage } from './AttendancePage';
import { DashboardPage } from './DashboardPage';
import { MessagesPage } from './MessagesPage';
import { PaymentsPage } from './PaymentsPage';
import { StudentDrawer } from './StudentDrawer';
import { StudentsPage } from './StudentsPage';
import './school.css';
import { SchoolContext } from './useSchool';

type Tab = 'dashboard' | 'students' | 'attendance' | 'announcements' | 'payments' | 'messages' | 'institution';

const tabs: { key: Tab; label: string; icon: string }[] = [
  { key: 'dashboard', label: 'Panel', icon: '◧' },
  { key: 'students', label: 'Öğrenciler', icon: '☺' },
  { key: 'attendance', label: 'Yoklama', icon: '✓' },
  { key: 'announcements', label: 'Duyurular', icon: '✉' },
  { key: 'payments', label: 'Tahsilat', icon: '₺' },
  { key: 'messages', label: 'Veli mesajı', icon: '✨' },
  { key: 'institution', label: 'Kurum', icon: '⌂' },
];

export interface SchoolShellProps {
  client: OkulClient;
  loadInstitution: () => Promise<InstitutionResult>;
  onSessionEnded: (expired: boolean) => void;
  onLogout: () => void;
  onChangePassword: () => void;
  notice?: string;
}

/** Demo okul paneli (AGENTS.md, Demo modu): giriş sonrası kabuk — kurum adı başlıkta, sekmeli içerik. */
export function SchoolShell({ client, loadInstitution, onSessionEnded, onLogout, onChangePassword, notice }: SchoolShellProps) {
  const [institution, setInstitution] = useState<InstitutionResult | { kind: 'loading' }>({ kind: 'loading' });
  const [tab, setTab] = useState<Tab>('dashboard');
  const [openStudent, setOpenStudent] = useState<string | null>(null);
  const [messageFor, setMessageFor] = useState<string | undefined>(undefined);
  const [toastText, setToastText] = useState<string | null>(notice ?? null);

  useEffect(() => {
    let active = true;
    void loadInstitution().then((r) => active && setInstitution(r));
    return () => {
      active = false;
    };
  }, [loadInstitution]);

  useEffect(() => {
    if (institution.kind === 'unauthenticated') onSessionEnded(institution.expired);
  }, [institution, onSessionEnded]);

  useLayoutEffect(() => {
    document.title = institution.kind === 'ok' ? institution.institution.name : 'Okul';
  }, [institution]);

  useEffect(() => {
    if (!toastText) return;
    const t = setTimeout(() => setToastText(null), 4500);
    return () => clearTimeout(t);
  }, [toastText]);

  const toast = useCallback((m: string) => setToastText(m), []);
  const context = useMemo(() => ({ client, onSessionEnded, toast }), [client, onSessionEnded, toast]);
  const closeStudent = useCallback(() => setOpenStudent(null), []);

  if (institution.kind === 'loading') {
    return <p role="status" aria-busy="true" className="empty">Yükleniyor…</p>;
  }
  if (institution.kind === 'unauthenticated') {
    return null;
  }
  if (institution.kind === 'error') {
    return (
      <main className="content">
        <p role="alert" className="alert">Bilgiler alınamadı, tekrar deneyin.</p>
      </main>
    );
  }
  const kurum = institution.institution;

  return (
    <SchoolContext.Provider value={context}>
      <div className="shell">
        <nav className="side" aria-label="Ana menü">
          <div className="brand">
            <h1>{kurum.name}</h1>
            <p>Okul yönetim paneli</p>
          </div>
          {tabs.map((t) => (
            <button key={t.key} type="button" className="nav-btn" aria-current={tab === t.key ? 'page' : undefined}
              onClick={() => { setTab(t.key); if (t.key !== 'messages') setMessageFor(undefined); }}>
              <span className="nav-icon" aria-hidden>{t.icon}</span>{t.label}
            </button>
          ))}
          <div className="side-foot">
            <button type="button" className="nav-btn" onClick={onChangePassword}>Şifremi değiştir</button>
            <button type="button" className="nav-btn" onClick={onLogout}>Çıkış yap</button>
          </div>
        </nav>

        <main className="content">
          {tab === 'dashboard' && <DashboardPage onOpenStudent={setOpenStudent} go={setTab} />}
          {tab === 'students' && <StudentsPage onOpenStudent={setOpenStudent} />}
          {tab === 'attendance' && <AttendancePage />}
          {tab === 'announcements' && <AnnouncementsPage />}
          {tab === 'payments' && <PaymentsPage onOpenStudent={setOpenStudent} />}
          {tab === 'messages' && <MessagesPage key={messageFor ?? 'none'} initialStudentId={messageFor} />}
          {tab === 'institution' && (
            <>
              <div className="page-head"><div><h2>Kurum</h2><p>Kurum kodu ve şubeler.</p></div></div>
              <div className="card">
                <h3>Şubeler</h3>
                {kurum.campuses.length === 0 ? <p className="empty">Henüz şube tanımlanmamış.</p> : (
                  <ul className="list" aria-label="Şubeler">{kurum.campuses.map((c) => <li key={c.id}>{c.name}</li>)}</ul>
                )}
              </div>
            </>
          )}
        </main>
      </div>

      {openStudent && (
        <StudentDrawer studentId={openStudent} onClose={closeStudent}
          onWriteMessage={(id) => { setOpenStudent(null); setMessageFor(id); setTab('messages'); }} />
      )}
      {toastText && <div className="toast" role="status">{toastText}</div>}
    </SchoolContext.Provider>
  );
}
