import { useState } from 'react';
import { dateText, type Installment, money } from './types';
import { useCall, useLoad, useSchool } from './useSchool';
import { Failed, InstallmentPill, Loading } from './ui';

const filters = [
  ['overdue', 'Gecikmiş'],
  ['upcoming', 'Önümüzdeki 30 gün'],
  ['paid', 'Ödenmiş'],
  ['all', 'Tümü'],
] as const;

export function PaymentsPage({ onOpenStudent }: { onOpenStudent: (id: string) => void }) {
  const call = useCall();
  const { toast } = useSchool();
  const [filter, setFilter] = useState<(typeof filters)[number][0]>('overdue');
  const [list, reload] = useLoad<Installment[]>(`/school/installments?filter=${filter}`);
  const [busy, setBusy] = useState<string | null>(null);

  async function pay(i: Installment) {
    setBusy(i.id);
    const result = await call<Installment>('POST', `/school/installments/${i.id}/pay`);
    setBusy(null);
    if (result.kind === 'ok') {
      toast(`✓ ${i.studentName} — ${money(i.remainingKurus)} tahsil edildi. Makbuz ${result.data.receiptNo}`);
      reload();
    } else if (result.kind === 'invalid') {
      toast(result.message);
    } else if (result.kind === 'error') {
      toast('Tahsilat kaydedilemedi.');
    }
  }

  const total = list.kind === 'ok' ? list.data.reduce((sum, i) => sum + (filter === 'paid' ? i.paidKurus : i.remainingKurus), 0) : 0;

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Tahsilat</h2>
          <p>Taksitler, gecikmeler ve makbuzlar. Online ödeme ve otomatik hatırlatma bu demoda simüle edilir.</p>
        </div>
      </div>
      <div className="toolbar">
        <div className="seg" role="group" aria-label="Filtre">
          {filters.map(([key, label]) => (
            <button key={key} type="button" className="excused" aria-pressed={filter === key} onClick={() => setFilter(key)}>{label}</button>
          ))}
        </div>
        {list.kind === 'ok' && <span className="muted small">{list.data.length} taksit · {filter === 'paid' ? 'tahsil edilen' : 'kalan'} <b>{money(total)}</b></span>}
      </div>
      <div className="card">
        {list.kind === 'loading' && <Loading />}
        {list.kind === 'error' && <Failed message={list.message} retry={reload} />}
        {list.kind === 'ok' && (list.data.length === 0 ? <p className="empty">Bu filtrede taksit yok.</p> : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>Öğrenci</th><th>Sınıf</th><th>Taksit</th><th>Vade</th><th className="num">Tutar</th><th className="num">Kalan</th><th>Durum</th><th></th></tr>
              </thead>
              <tbody>
                {list.data.map((i) => (
                  <tr key={i.id}>
                    <td><button className="btn ghost small" style={{ padding: 0, color: 'var(--text)', fontWeight: 600 }} onClick={() => onOpenStudent(i.studentId)}>{i.studentName}</button></td>
                    <td>{i.className}</td>
                    <td className="muted">{i.number}/10</td>
                    <td>{dateText(i.dueDate)}</td>
                    <td className="num">{money(i.amountKurus)}</td>
                    <td className="num">{i.remainingKurus > 0 ? <b>{money(i.remainingKurus)}</b> : '—'}</td>
                    <td><InstallmentPill status={i.status} /></td>
                    <td style={{ textAlign: 'right' }}>
                      {i.remainingKurus > 0 ? (
                        <button className="btn small" disabled={busy === i.id} onClick={() => void pay(i)}>{busy === i.id ? '…' : 'Tahsil et'}</button>
                      ) : (
                        <span className="small muted">{i.receiptNo}</span>
                      )}
                    </td>
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
