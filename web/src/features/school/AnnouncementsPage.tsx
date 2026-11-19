import { useState } from 'react';
import { type Announcement, type ClassGroup, dateTimeText } from './types';
import { useCall, useLoad, useSchool } from './useSchool';
import { Failed, Loading } from './ui';

export function AnnouncementsPage() {
  const call = useCall();
  const { toast } = useSchool();
  const [classes] = useLoad<ClassGroup[]>('/school/classes');
  const [list, reload] = useLoad<Announcement[]>('/school/announcements');
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [target, setTarget] = useState('');
  const [sending, setSending] = useState(false);

  async function send(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || !body.trim()) {
      toast('Başlık ve metin gerekli.');
      return;
    }
    setSending(true);
    const result = await call<Announcement>('POST', '/school/announcements', { title, body, classGroupId: target || null });
    setSending(false);
    if (result.kind === 'ok') {
      toast(`✓ Duyuru ${result.data.recipientCount} veliye gönderildi.`);
      setTitle('');
      setBody('');
      reload();
    } else if (result.kind === 'invalid') {
      toast(result.message);
    } else if (result.kind === 'error') {
      toast('Gönderilemedi, tekrar deneyin.');
    }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Duyurular</h2>
          <p>Okula veya tek bir sınıfa duyuru gönderin; kimin okuduğunu görün.</p>
        </div>
      </div>
      <div className="grid two">
        <form className="card" onSubmit={(e) => void send(e)} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <h3 style={{ margin: 0 }}>Yeni duyuru</h3>
          <label className="field">Kime
            <select className="select" value={target} onChange={(e) => setTarget(e.target.value)}>
              <option value="">Tüm okul velileri</option>
              {classes.kind === 'ok' && classes.data.map((c) => <option key={c.id} value={c.id}>{c.name} velileri ({c.studentCount})</option>)}
            </select>
          </label>
          <label className="field">Başlık
            <input className="input" value={title} maxLength={160} onChange={(e) => setTitle(e.target.value)} placeholder="Örn. Gezi izni — 5 Aralık" />
          </label>
          <label className="field">Metin
            <textarea className="textarea" value={body} maxLength={4000} onChange={(e) => setBody(e.target.value)} placeholder="Değerli velilerimiz…" />
          </label>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button className="btn" type="submit" disabled={sending}>{sending ? 'Gönderiliyor…' : 'Gönder'}</button>
          </div>
        </form>

        <section className="card">
          <h3>Gönderilenler</h3>
          {list.kind === 'loading' && <Loading />}
          {list.kind === 'error' && <Failed message={list.message} retry={reload} />}
          {list.kind === 'ok' && list.data.map((a) => {
            const rate = a.recipientCount ? a.readCount / a.recipientCount : 0;
            return (
              <div key={a.id} className="msg">
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10 }}>
                  <b>{a.title}</b>
                  <span className={`pill ${rate >= 0.8 ? 'ok' : rate >= 0.5 ? 'warn' : 'muted'}`}>{a.readCount}/{a.recipientCount} okundu</span>
                </div>
                <span className="small muted">{a.target} · {dateTimeText(a.createdAt)}</span>
                <p>{a.body}</p>
              </div>
            );
          })}
        </section>
      </div>
    </>
  );
}
