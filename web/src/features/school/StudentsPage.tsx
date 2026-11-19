import { useEffect, useState } from 'react';
import { type ClassGroup, money, type StudentListItem } from './types';
import { useLoad } from './useSchool';
import { Failed, Loading } from './ui';

export function StudentsPage({ onOpenStudent }: { onOpenStudent: (id: string) => void }) {
  const [classes] = useLoad<ClassGroup[]>('/school/classes');
  const [classId, setClassId] = useState('');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');

  useEffect(() => {
    const t = setTimeout(() => setQuery(search), 250);
    return () => clearTimeout(t);
  }, [search]);

  const params = new URLSearchParams();
  if (classId) params.set('classId', classId);
  if (query.trim()) params.set('q', query.trim());
  const [students, reload] = useLoad<StudentListItem[]>(`/school/students?${params.toString()}`);

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Öğrenciler</h2>
          <p>Öğrenci kartı, veli, sağlık notu, devamsızlık ve ödeme durumu tek yerde.</p>
        </div>
      </div>
      <div className="toolbar">
        <input className="input" placeholder="Ad, numara veya veli ara…" aria-label="Öğrenci ara" value={search} onChange={(e) => setSearch(e.target.value)} />
        <select className="select" aria-label="Sınıf" value={classId} onChange={(e) => setClassId(e.target.value)}>
          <option value="">Tüm sınıflar</option>
          {classes.kind === 'ok' && classes.data.map((c) => <option key={c.id} value={c.id}>{c.name} · {c.teacherName}</option>)}
        </select>
        {students.kind === 'ok' && <span className="muted small">{students.data.length} öğrenci</span>}
      </div>
      <div className="card">
        {students.kind === 'loading' && <Loading />}
        {students.kind === 'error' && <Failed message={students.message} retry={reload} />}
        {students.kind === 'ok' && (students.data.length === 0 ? <p className="empty">Aramaya uyan öğrenci yok.</p> : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>No</th><th>Ad soyad</th><th>Sınıf</th><th>Veli</th><th className="num">Devamsızlık</th><th className="num">Geciken</th><th></th></tr>
              </thead>
              <tbody>
                {students.data.map((s) => (
                  <tr key={s.id} className="clickable" onClick={() => onOpenStudent(s.id)} tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && onOpenStudent(s.id)}>
                    <td className="muted">{s.number}</td>
                    <td><b>{s.fullName}</b></td>
                    <td>{s.className}</td>
                    <td className="muted">{s.guardianName}</td>
                    <td className="num">{s.absenceCount >= 3 ? <span className="pill bad">{s.absenceCount} gün</span> : `${s.absenceCount} gün`}</td>
                    <td className="num">{s.overdueKurus > 0 ? <span className="pill bad">{money(s.overdueKurus)}</span> : <span className="muted">—</span>}</td>
                    <td>{s.hasHealthNote && <span className="pill warn" title="Sağlık notu / alerji var">Sağlık</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </>
  );
}
