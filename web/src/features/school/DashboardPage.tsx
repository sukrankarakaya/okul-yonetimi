import { type Dashboard, dateText, money } from './types';
import { useLoad } from './useSchool';
import { Failed, Loading } from './ui';

export function DashboardPage({ onOpenStudent, go }: { onOpenStudent: (id: string) => void; go: (tab: 'attendance' | 'payments' | 'announcements') => void }) {
  const [state, reload] = useLoad<Dashboard>('/school/dashboard');
  if (state.kind === 'loading') return <Loading />;
  if (state.kind === 'error') return <Failed message={state.message} retry={reload} />;
  const d = state.data;
  const collectRate = d.expectedThisMonthKurus === 0 ? 0 : d.collectedThisMonthKurus / d.expectedThisMonthKurus;

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Günaydın 👋</h2>
          <p>{dateText(d.today, true)} · {d.studentCount} öğrenci, {d.classCount} sınıf</p>
        </div>
      </div>

      <div className="grid kpis">
        <div className="card">
          <div className="kpi-label">Bugün devamsız</div>
          <div className="kpi-value" style={{ color: d.absentToday ? 'var(--bad)' : undefined }}>{d.absentToday}</div>
          <div className="kpi-sub">{d.lateToday} geç · velilere otomatik bildirim gitti</div>
        </div>
        <div className="card">
          <div className="kpi-label">Yoklaması alınmamış sınıf</div>
          <div className="kpi-value" style={{ color: d.classesWithoutAttendance ? 'var(--warn)' : 'var(--ok)' }}>{d.classesWithoutAttendance}</div>
          <div className="kpi-sub">
            {d.classesWithoutAttendance ? <button className="btn ghost small" style={{ padding: 0 }} onClick={() => go('attendance')}>Yoklamaya git →</button> : 'Tüm sınıflar tamam'}
          </div>
        </div>
        <div className="card">
          <div className="kpi-label">Geciken tahsilat</div>
          <div className="kpi-value" style={{ color: 'var(--bad)' }}>{money(d.overdueKurus)}</div>
          <div className="kpi-sub">{d.overdueInstallmentCount} taksit vadesini geçti</div>
        </div>
        <div className="card">
          <div className="kpi-label">Bu ay tahsil edilen</div>
          <div className="kpi-value">{money(d.collectedThisMonthKurus)}</div>
          <div className="kpi-sub">beklenen {money(d.expectedThisMonthKurus)} · %{Math.round(collectRate * 100)}</div>
          <div className="bar"><span style={{ width: `${Math.min(100, collectRate * 100)}%` }} /></div>
        </div>
        <div className="card">
          <div className="kpi-label">Duyuru okunma oranı</div>
          <div className="kpi-value">%{Math.round(d.announcementReadRate * 100)}</div>
          <div className="kpi-sub">son 7 günde {d.announcementsLast7Days} duyuru</div>
          <div className="bar"><span style={{ width: `${d.announcementReadRate * 100}%` }} /></div>
        </div>
      </div>

      <div className="grid two" style={{ marginTop: 16 }}>
        <section className="card">
          <h3>Bugün okula gelmeyenler</h3>
          {d.absentStudents.length === 0 ? (
            <p className="empty">Bugün devamsız öğrenci yok.</p>
          ) : (
            <ul className="list">
              {d.absentStudents.map((s) => (
                <li key={s.id}>
                  <button className="btn ghost small" style={{ padding: 0, color: 'var(--text)' }} onClick={() => onOpenStudent(s.id)}>
                    {s.fullName}
                  </button>
                  <span className="muted small">{s.className} · toplam {s.absenceCount} gün</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="card">
          <h3>En çok geciken ödemeler</h3>
          {d.topOverdue.length === 0 ? (
            <p className="empty">Geciken ödeme yok. 🎉</p>
          ) : (
            <ul className="list">
              {d.topOverdue.map((i) => (
                <li key={i.id}>
                  <button className="btn ghost small" style={{ padding: 0, color: 'var(--text)' }} onClick={() => onOpenStudent(i.studentId)}>
                    {i.studentName}
                  </button>
                  <span className="small"><span className="muted">{i.number}. taksit · {dateText(i.dueDate)}</span> <b style={{ color: 'var(--bad)' }}>{money(i.remainingKurus)}</b></span>
                </li>
              ))}
            </ul>
          )}
          <button className="btn secondary small" style={{ marginTop: 12 }} onClick={() => go('payments')}>Tüm tahsilat →</button>
        </section>

        <section className="card">
          <h3>Son duyurular</h3>
          <ul className="list">
            {d.recentAnnouncements.map((a) => (
              <li key={a.id}>
                <span>{a.title}</span>
                <span className="pill info">%{a.recipientCount ? Math.round((a.readCount / a.recipientCount) * 100) : 0} okundu</span>
              </li>
            ))}
          </ul>
          <button className="btn secondary small" style={{ marginTop: 12 }} onClick={() => go('announcements')}>Duyuru gönder →</button>
        </section>
      </div>
    </>
  );
}
