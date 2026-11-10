// Anzeige-Helfer: Datumsformat, Fristen, Schweregrad-Reihenfolge, EPSS.
import type { Severity } from './types';

const dateTime = new Intl.DateTimeFormat('de-DE', {
  dateStyle: 'medium',
  timeStyle: 'short',
});
const dateFmt = new Intl.DateTimeFormat('de-DE', { dateStyle: 'medium' });

export function formatDateTime(iso: string | null): string {
  if (!iso) return '–';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '–' : dateTime.format(d);
}

export function formatDate(iso: string | null): string {
  if (!iso) return '–';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '–' : dateFmt.format(d);
}

/** Menschlich lesbare Fristangabe für Tickets (C4). */
export function dueLabel(dueAt: string | null, now: Date = new Date()): string {
  if (!dueAt) return 'keine Frist';
  const due = new Date(dueAt);
  if (Number.isNaN(due.getTime())) return 'keine Frist';
  const diffMs = due.getTime() - now.getTime();
  const abs = Math.abs(diffMs);
  const hours = Math.floor(abs / 3_600_000);
  const minutes = Math.floor((abs % 3_600_000) / 60_000);
  const span = hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
  return diffMs >= 0 ? `noch ${span} · bis ${formatDateTime(dueAt)}` : `seit ${span} überfällig · Frist ${formatDateTime(dueAt)}`;
}

export const severityOrder: Record<Severity, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

export const severityLabels: Record<Severity, string> = {
  Critical: 'Kritisch',
  High: 'Hoch',
  Medium: 'Mittel',
  Low: 'Niedrig',
};

export const statusLabels: Record<string, string> = {
  Pending: 'Wartend',
  Scanning: 'Läuft',
  Scanned: 'Abgeschlossen',
  Failed: 'Fehlgeschlagen',
  Open: 'Offen',
  InProgress: 'In Bearbeitung',
  Escalated: 'Eskaliert',
  Closed: 'Geschlossen',
  RiskAccepted: 'Risiko akzeptiert',
};

export const verdictLabels: Record<string, string> = {
  Go: 'Go – Freigabe möglich',
  GoWithConditions: 'Go mit Auflagen',
  NoGo: 'NoGo – Freigabe gesperrt',
};

/** EPSS-Wert (0..1) als Prozent, null bleibt leer. */
export function epssPercent(epss: number | null): string {
  if (epss === null || Number.isNaN(epss)) return '–';
  return `${(epss * 100).toFixed(1)} %`;
}

/** Nur http/https als Link erlauben (E5: sicherer Link). */
export function safeHttpUrl(raw: string): string | null {
  try {
    const u = new URL(raw);
    if (u.protocol === 'http:' || u.protocol === 'https:') return u.toString();
    return null;
  } catch {
    return null;
  }
}
