import { useEffect } from 'react';
import { attendanceLabels, dateText, dateTimeText, money, type StudentDetail } from './types';
import { useLoad } from './useSchool';
import { Failed, InstallmentPill, Loading } from './ui';

export function StudentDrawer({ studentId, onClose, onWriteMessage }: { studentId: string; onClose: () => void; onWriteMessage: (id: string) => void }) {
  const [state, reload] = useLoad<StudentDetail>(`/school/students/${studentId}`);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div className="drawer-back" onClick={onClose}>
      <aside className="drawer" role="dialog" aria-modal="true" aria-label="Öğrenci kartı" onClick={(e) => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span className="muted small">Öğrenci kartı</span>
          <button className="btn ghost small" onClick={onClose} aria-label="Kapat">✕</button>
        </div>
        {state.kind === 'loading' && <Loading />}
        {state.kind === 'error' && <Failed message={state.message} retry={reload} />}
        {state.kind === 'ok' && <Detail s={state.data} onWriteMessage={onWriteMessage} />}
      </aside>
    </div>
  );
}

function Detail({ s, onWriteMessage }: { s: StudentDetail; onWriteMessage: (id: string) => void }) {
  const overdue = s.installments.filter((i) => i.status === 'overdue').reduce((sum, i) => sum + i.remainingKurus, 0);
  const paid = s.installments.reduce((sum, i) => sum + i.paidKurus, 0);
  const total = s.installments.reduce((sum, i) => sum + i.amountKurus, 0);

  return (
    <>
      <div>
        <h2 style={{ margin: 0, fontSize: 22 }}>{s.firstName} {s.lastName}</h2>
        <p className="muted" style={{ margin: '4px 0 0' }}>{s.className} · No {s.number} · Sınıf öğretmeni {s.teacherName}</p>
      </div>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        <button className="btn ai small" onClick={() => onWriteMessage(s.id)}>✨ Veliye mesaj yaz</button>
        {!s.photoConsent && <span className="pill warn">Fotoğraf izni yok</span>}
        {overdue > 0 && <span className="pill bad">{money(overdue)} gecikmiş</span>}
      </div>

      {(s.healthNote || s.allergies) && (
        <section className="card" style={{ borderColor: 'var(--warn)' }}>
          <h3>⚕ Sağlık</h3>
          <dl className="kv">
            {s.healthNote && (<><dt>Not</dt><dd>{s.healthNote}</dd></>)}
            {s.allergies && (<><dt>Alerji</dt><dd><b>{s.allergies}</b></dd></>)}
          </dl>
        </section>
      )}

      <section className="card">
        <h3>Veli ve teslim</h3>
        <dl className="kv">
          <dt>Veli</dt><dd>{s.guardianName} ({s.guardianRelation})</dd>
          <dt>Telefon</dt><dd>{s.guardianPhone}</dd>
          <dt>Teslim alabilir</dt><dd>{s.authorizedPickup ?? <span className="muted">Yalnızca veli</span>}</dd>
          <dt>Doğum tarihi</dt><dd>{new Date(`${s.birthDate}T12:00:00`).toLocaleDateString('tr-TR')}</dd>
        </dl>
      </section>

      <section className="card">
        <h3>Devamsızlık — son 10 gün</h3>
        <div className="days" aria-label="Son 10 günün yoklaması">
          {s.recentAttendance.map((a) => (
            <span key={a.date} className={`day ${a.status}`} title={`${dateText(a.date, true)}: ${attendanceLabels[a.status]}`}>
              {new Date(`${a.date}T12:00:00`).getDate()}
            </span>
          ))}
        </div>
        <p className="small muted" style={{ marginBottom: 0 }}>Toplam {s.absenceCount} gün devamsız, {s.lateCount} kez geç.</p>
      </section>

      <section className="card">
        <h3>Ödeme planı</h3>
        <p className="small muted" style={{ marginTop: -6 }}>Ödenen {money(paid)} / {money(total)}</p>
        <div className="bar" style={{ marginBottom: 10 }}><span style={{ width: `${total ? (paid / total) * 100 : 0}%` }} /></div>
        <table className="table">
          <tbody>
            {s.installments.map((i) => (
              <tr key={i.id}>
                <td className="muted">{i.number}.</td>
                <td>{dateText(i.dueDate)}</td>
                <td className="num">{money(i.amountKurus)}</td>
                <td><InstallmentPill status={i.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {s.messages.length > 0 && (
        <section className="card">
          <h3>Veliye gönderilen mesajlar</h3>
          {s.messages.map((m) => (
            <div key={m.id} className="msg">
              <span className="small muted">{dateTimeText(m.sentAt)} · {m.sentBy}</span> {m.aiAssisted && <span className="pill ai">AI destekli</span>}
              <p>{m.body}</p>
            </div>
          ))}
        </section>
      )}
    </>
  );
}
