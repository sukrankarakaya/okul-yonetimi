import { useState } from 'react';
import { type AttendanceSheet, type AttendanceStatus, attendanceLabels, type ClassGroup, dateText, todayIso } from './types';
import { useCall, useLoad, useSchool } from './useSchool';
import { Failed, Loading } from './ui';

const statuses: AttendanceStatus[] = ['present', 'absent', 'late', 'excused'];

export function AttendancePage() {
  const [classes] = useLoad<ClassGroup[]>('/school/classes');
  const [chosen, setClassId] = useState('');
  const [date, setDate] = useState(todayIso());
  // Varsayılan: son sınıf (örnek veride bugünkü yoklaması alınmamış olan).
  const classId = chosen || (classes.kind === 'ok' ? (classes.data.at(-1)?.id ?? '') : '');

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Yoklama</h2>
          <p>Herkes "Var" olarak başlar — yalnızca gelmeyenlere dokunun. "Yok" işaretlenen öğrencinin velisine bildirim gider.</p>
        </div>
      </div>
      <div className="toolbar">
        <select className="select" aria-label="Sınıf" value={classId} onChange={(e) => setClassId(e.target.value)}>
          {classes.kind === 'ok' && classes.data.map((c) => <option key={c.id} value={c.id}>{c.name} · {c.teacherName}</option>)}
        </select>
        <input className="input" type="date" aria-label="Tarih" value={date} max={todayIso()} onChange={(e) => setDate(e.target.value)} style={{ width: 'auto' }} />
      </div>
      {classId ? <Sheet key={`${classId}-${date}`} classId={classId} date={date} /> : <Loading />}
    </>
  );
}

function Sheet({ classId, date }: { classId: string; date: string }) {
  const call = useCall();
  const { toast } = useSchool();
  const [sheet, reload] = useLoad<AttendanceSheet>(`/school/attendance?classId=${classId}&date=${date}`);
  const [overrides, setMarks] = useState<Record<string, AttendanceStatus>>({});
  const [saving, setSaving] = useState(false);

  if (sheet.kind === 'loading') return <Loading />;
  if (sheet.kind === 'error') return <Failed message={sheet.message} retry={reload} />;
  const s = sheet.data;
  // Herkes "Var" başlar; kayıtlı durum varsa o, öğretmenin dokunduğu satırda onun seçimi.
  const marks: Record<string, AttendanceStatus> = Object.fromEntries(
    s.rows.map((r) => [r.studentId, overrides[r.studentId] ?? r.status ?? 'present']),
  );
  const counts = statuses.map((st) => [st, Object.values(marks).filter((m) => m === st).length] as const);

  async function save() {
    setSaving(true);
    const result = await call<{ saved: number; absent: number; guardiansNotified: number }>('POST', '/school/attendance', {
      classGroupId: classId,
      date,
      entries: Object.entries(marks).map(([studentId, status]) => ({ studentId, status })),
    });
    setSaving(false);
    if (result.kind === 'ok') {
      const r = result.data;
      toast(`✓ ${s.className} yoklaması kaydedildi. ${r.absent} devamsız${r.guardiansNotified ? ` · ${r.guardiansNotified} veliye bildirim gönderildi` : ''}.`);
      reload();
    } else if (result.kind === 'invalid') {
      toast(result.message);
    } else if (result.kind === 'error') {
      toast('Kaydedilemedi, tekrar deneyin.');
    }
  }

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', alignItems: 'center', marginBottom: 8 }}>
        <div>
          <h3 style={{ margin: 0 }}>{s.className} · {dateText(s.date, true)}</h3>
          <span className="small muted">{s.taken ? 'Bu günün yoklaması alınmış — düzenleyebilirsiniz.' : 'Bu günün yoklaması henüz alınmadı.'}</span>
        </div>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {counts.map(([st, n]) => <span key={st} className={`pill ${st === 'present' ? 'ok' : st === 'absent' ? 'bad' : st === 'late' ? 'warn' : 'info'}`}>{attendanceLabels[st]}: {n}</span>)}
        </div>
      </div>
      <div className="table-wrap">
        <table className="table">
          <tbody>
            {s.rows.map((r) => (
              <tr key={r.studentId}>
                <td className="muted" style={{ width: 50 }}>{r.number}</td>
                <td><b>{r.fullName}</b>{r.guardianNotified && <span className="small muted"> · veliye bildirildi</span>}</td>
                <td style={{ textAlign: 'right' }}>
                  <div className="seg" role="group" aria-label={`${r.fullName} yoklama`}>
                    {statuses.map((st) => (
                      <button key={st} type="button" className={st} aria-pressed={marks[r.studentId] === st} onClick={() => setMarks((m) => ({ ...m, [r.studentId]: st }))}>
                        {attendanceLabels[st]}
                      </button>
                    ))}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 14 }}>
        <button className="btn" onClick={() => void save()} disabled={saving}>{saving ? 'Kaydediliyor…' : 'Yoklamayı kaydet'}</button>
      </div>
    </div>
  );
}
