import { useState } from 'react';
import { type ClassGroup, dateTimeText, type DraftResult, type GuardianMessage, type StudentListItem } from './types';
import { useCall, useLoad, useSchool } from './useSchool';
import { Failed, Loading } from './ui';

const examples = [
  'bugün matematikte çok iyiydi, kesirleri arkadaşlarına anlattı',
  'son 2 haftadır ödev getirmiyor derste dalgın telefonla ilgili olabilir konuşalım',
  'teneffüste arkadaşıyla tartıştı, sonra barıştılar ama haberiniz olsun',
];

export function MessagesPage({ initialStudentId }: { initialStudentId?: string }) {
  const call = useCall();
  const { toast } = useSchool();
  const [classes] = useLoad<ClassGroup[]>('/school/classes');
  const [chosenClass, setClassId] = useState('');
  const [students] = useLoad<StudentListItem[]>('/school/students');
  const [studentId, setStudentId] = useState(initialStudentId ?? '');
  const [note, setNote] = useState('');
  const [draft, setDraft] = useState<DraftResult | null>(null);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState<'draft' | 'send' | null>(null);
  const [sent, reloadSent] = useLoad<GuardianMessage[]>('/school/messages');

  // Varsayılan sınıf: öğrenci kartından gelindiyse onun sınıfı, değilse ilk sınıf.
  const initialClass = students.kind === 'ok' ? students.data.find((x) => x.id === initialStudentId)?.classGroupId : undefined;
  const classId = chosenClass || initialClass || (classes.kind === 'ok' ? (classes.data[0]?.id ?? '') : '');

  const student = students.kind === 'ok' ? students.data.find((s) => s.id === studentId) : undefined;

  async function makeDraft() {
    if (!studentId || !note.trim()) {
      toast('Öğrenci seçin ve kısa bir not yazın.');
      return;
    }
    setBusy('draft');
    const result = await call<DraftResult>('POST', '/school/messages/draft', { studentId, teacherNote: note });
    setBusy(null);
    if (result.kind === 'ok') {
      setDraft(result.data);
      setText(result.data.draft);
    } else if (result.kind === 'invalid') {
      toast(result.message);
    } else if (result.kind === 'error') {
      toast('Taslak oluşturulamadı.');
    }
  }

  async function send() {
    setBusy('send');
    const result = await call<GuardianMessage>('POST', '/school/messages', { studentId, body: text, aiAssisted: draft !== null });
    setBusy(null);
    if (result.kind === 'ok') {
      toast(`✓ Mesaj ${student?.guardianName ?? 'veliye'} gönderildi.`);
      setDraft(null);
      setText('');
      setNote('');
      reloadSent();
    } else if (result.kind === 'invalid') {
      toast(result.message);
    } else if (result.kind === 'error') {
      toast('Gönderilemedi.');
    }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Veli mesajı <span className="pill ai" style={{ verticalAlign: 'middle' }}>✨ AI</span></h2>
          <p>Aklınızdakini kısaca yazın; AI kibar bir veli mesajına çevirsin. Siz onaylamadan hiçbir şey gönderilmez.</p>
        </div>
      </div>
      <div className="grid two">
        <section className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 10 }}>
            <label className="field">Sınıf
              <select className="select" value={classId} onChange={(e) => { setClassId(e.target.value); setStudentId(''); }}>
                {classes.kind === 'ok' && classes.data.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </label>
            <label className="field">Öğrenci
              <select className="select" value={studentId} onChange={(e) => setStudentId(e.target.value)}>
                <option value="">Seçin…</option>
                {students.kind === 'ok' && students.data.filter((s) => !classId || s.classGroupId === classId).map((s) => (
                  <option key={s.id} value={s.id}>{s.fullName} — veli {s.guardianName}</option>
                ))}
              </select>
            </label>
          </div>
          <label className="field">Öğretmen notu (dağınık yazabilirsiniz)
            <textarea className="textarea" value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} placeholder="ör. son 2 haftadır ödev getirmiyor, derste dalgın…" />
          </label>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {examples.map((x) => <button key={x} type="button" className="btn secondary small" onClick={() => setNote(x)} title={x}>{x.slice(0, 28)}…</button>)}
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button className="btn ai" disabled={busy !== null} onClick={() => void makeDraft()}>{busy === 'draft' ? 'Yazılıyor…' : '✨ Taslak oluştur'}</button>
          </div>

          {draft && (
            <div className="ai-box">
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
                <b>Taslak — düzenleyip onaylayın</b>
                <span className="pill ai">{draft.engine}</span>
              </div>
              <textarea className="textarea" style={{ minHeight: 200 }} value={text} onChange={(e) => setText(e.target.value)} aria-label="Mesaj taslağı" />
              <div>
                <span className="small muted">Kullanılan bilgiler:</span>
                <ul className="sources">{draft.sources.map((s) => <li key={s}>{s}</li>)}</ul>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
                <button className="btn secondary" onClick={() => setDraft(null)}>Vazgeç</button>
                <button className="btn" disabled={busy !== null || !text.trim()} onClick={() => void send()}>{busy === 'send' ? 'Gönderiliyor…' : 'Onayla ve gönder'}</button>
              </div>
            </div>
          )}
        </section>

        <section className="card">
          <h3>Gönderilen mesajlar</h3>
          {sent.kind === 'loading' && <Loading />}
          {sent.kind === 'error' && <Failed message={sent.message} retry={reloadSent} />}
          {sent.kind === 'ok' && (sent.data.length === 0 ? <p className="empty">Henüz mesaj gönderilmedi.</p> : sent.data.map((m) => (
            <div key={m.id} className="msg">
              <b>{m.studentName}</b> <span className="small muted">· {dateTimeText(m.sentAt)} · {m.sentBy}</span> {m.aiAssisted && <span className="pill ai">AI destekli</span>}
              <p>{m.body}</p>
            </div>
          )))}
        </section>
      </div>
    </>
  );
}
