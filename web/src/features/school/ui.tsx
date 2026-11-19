import { attendanceLabels, type AttendanceStatus, type InstallmentStatus, installmentLabels } from './types';

export function Loading() {
  return (
    <p role="status" aria-busy="true" className="empty">
      Yükleniyor…
    </p>
  );
}

export function Failed({ message, retry }: { message: string; retry: () => void }) {
  return (
    <div className="card" style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
      <p role="alert" className="alert" style={{ margin: 0 }}>{message}</p>
      <button className="btn secondary" onClick={retry}>Tekrar dene</button>
    </div>
  );
}

const installmentTone: Record<InstallmentStatus, string> = { paid: 'ok', overdue: 'bad', partial: 'warn', pending: 'muted' };

export function InstallmentPill({ status }: { status: InstallmentStatus }) {
  return <span className={`pill ${installmentTone[status]}`}>{installmentLabels[status]}</span>;
}

const attendanceTone: Record<AttendanceStatus, string> = { present: 'ok', absent: 'bad', late: 'warn', excused: 'info' };

export function AttendancePill({ status }: { status: AttendanceStatus }) {
  return <span className={`pill ${attendanceTone[status]}`}>{attendanceLabels[status]}</span>;
}
