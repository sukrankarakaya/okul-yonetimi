// Demo okul paneli (AGENTS.md, Demo modu) — backend `Okul.Modules.School.Application` DTO'larının aynası.

export interface ClassGroup { id: string; name: string; gradeLevel: number; teacherName: string; studentCount: number }

export interface StudentListItem {
  id: string; number: number; fullName: string; classGroupId: string; className: string; guardianName: string;
  absenceCount: number; overdueKurus: number; hasHealthNote: boolean;
}

export type InstallmentStatus = 'paid' | 'overdue' | 'partial' | 'pending';

export interface Installment {
  id: string; studentId: string; studentName: string; className: string; number: number; dueDate: string;
  amountKurus: number; paidKurus: number; remainingKurus: number; status: InstallmentStatus; receiptNo: string | null;
  paidAt: string | null;
}

export type AttendanceStatus = 'present' | 'absent' | 'late' | 'excused';

export interface GuardianMessage {
  id: string; studentId: string; studentName: string; body: string; aiAssisted: boolean; sentBy: string; sentAt: string;
}

export interface StudentDetail {
  id: string; number: number; firstName: string; lastName: string; classGroupId: string; className: string;
  teacherName: string; birthDate: string; healthNote: string | null; allergies: string | null; photoConsent: boolean;
  guardianName: string; guardianRelation: string; guardianPhone: string; authorizedPickup: string | null;
  recentAttendance: { date: string; status: AttendanceStatus }[]; absenceCount: number; lateCount: number;
  installments: Installment[]; messages: GuardianMessage[];
}

export interface AttendanceSheet {
  classGroupId: string; className: string; date: string; taken: boolean;
  rows: { studentId: string; number: number; fullName: string; status: AttendanceStatus | null; guardianNotified: boolean }[];
}

export interface Announcement {
  id: string; title: string; body: string; classGroupId: string | null; target: string; createdAt: string;
  recipientCount: number; readCount: number;
}

export interface Dashboard {
  today: string; studentCount: number; classCount: number; absentToday: number; lateToday: number;
  classesWithoutAttendance: number; overdueKurus: number; overdueInstallmentCount: number;
  collectedThisMonthKurus: number; expectedThisMonthKurus: number; announcementsLast7Days: number;
  announcementReadRate: number; absentStudents: StudentListItem[]; topOverdue: Installment[];
  recentAnnouncements: Announcement[];
}

export interface DraftResult { draft: string; engine: string; sources: string[] }

const tl = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 });

/** Kuruş (BR-2) → "₺18.000". */
export function money(kurus: number): string {
  return tl.format(Math.round(kurus / 100));
}

export function dateText(iso: string, withWeekday = false): string {
  const d = new Date(iso.length === 10 ? `${iso}T12:00:00` : iso);
  return d.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', ...(withWeekday ? { weekday: 'long' } : {}) });
}

export function dateTimeText(iso: string): string {
  return new Date(iso).toLocaleString('tr-TR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
}

export const attendanceLabels: Record<AttendanceStatus, string> = {
  present: 'Var', absent: 'Yok', late: 'Geç', excused: 'İzinli',
};

export const installmentLabels: Record<InstallmentStatus, string> = {
  paid: 'Ödendi', overdue: 'Gecikmiş', partial: 'Kısmi', pending: 'Bekliyor',
};

/** Bugünün tarihi Türkiye saatine göre (YYYY-MM-DD). */
export function todayIso(): string {
  return new Date(Date.now() + 3 * 3600_000).toISOString().slice(0, 10);
}
