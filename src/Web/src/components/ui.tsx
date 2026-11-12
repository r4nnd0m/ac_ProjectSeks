// Wiederverwendbare Bausteine mit modernem Design.

import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import type { Severity, Verdict } from '../types';
import { ApiError, MESSAGES, describeError } from '../api/http';
import { severityLabels, statusLabels, verdictLabels } from '../utils';
import { ToastProvider, useToast } from './ToastContext';
import { ConfirmDialog } from './ConfirmDialog';
import { Skeleton, SkeletonText, SkeletonBlock, SkeletonCard } from './Skeleton';
import { AdminTabs } from './AdminTabs';
import { Breadcrumbs } from './Breadcrumbs';
import { SortConfig, getSortIcon, sortData } from '../utils/sort';

export function Spinner({ label }: { label: string }) {
  return (
    <p className="hint" role="status">
      <span className="spinner" aria-hidden="true" /> {label}
    </p>
  );
}

export function EmptyState({ text }: { text: string }) {
  return (
    <div className="empty-state">
      <span className="empty-icon" aria-hidden="true">📋</span>
      <p className="hint empty">{text}</p>
    </div>
  );
}

export function ErrorBox({ error, children }: { error: unknown; children?: ReactNode }) {
  const message = describeError(error);
  return (
    <div className="errorbox" role="alert">
      <strong>Hinweis:</strong> {message}
      {children ? <div className="errorbox-extra">{children}</div> : null}
    </div>
  );
}

/** Für 403-Fälle mit dem Hinweis aus E2: verbindlich prüft das Backend. */
export function ForbiddenNote() {
  return (
    <ErrorBox error={new ApiError(403, MESSAGES.forbidden, 'forbidden')}>
      <span>Falls du glauben solltest, Zugang zu haben, wende dich an eine Admin-Person.</span>
    </ErrorBox>
  );
}

export function SeverityBadge({ severity }: { severity: Severity }) {
  return <span className={`badge sev-${severity}`}>{severityLabels[severity]}</span>;
}

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge st-${status}`}>{statusLabels[status] ?? status}</span>;
}

/**
 * Urteil als Plumbob-Ampel (E3). Ein Urteil wird nur bei Status „Scanned“
 * angezeigt; ein fehlendes Urteil ist nie grün.
 */
export function VerdictDisplay({ verdict, status }: { verdict: Verdict | null; status: string }) {
  let cls = 'verdict-none';
  let text = 'Kein Urteil vorhanden.';
  if (status === 'Pending' || status === 'Scanning') {
    text = 'Der Plumbob dreht sich noch …';
  } else if (status === 'Failed') {
    text = 'Der Scan ist fehlgeschlagen, bitte erneut versuchen.';
  } else if (status === 'Scanned' && verdict === 'Go') {
    cls = 'verdict-go';
    text = `${verdictLabels.Go}: Sul sul! Alles grün, ab ins Wochenende.`;
  } else if (status === 'Scanned' && verdict === 'GoWithConditions') {
    cls = 'verdict-gwc';
    text = `${verdictLabels.GoWithConditions}: Hmm, komisch … ein paar Dinge gehören noch erledigt.`;
  } else if (status === 'Scanned' && verdict === 'NoGo') {
    cls = 'verdict-nogo';
    text = `${verdictLabels.NoGo}: Alle Mann an Deck: Dieses Release bleibt in der Garage.`;
  }
  return (
    <p className={`verdict ${cls}`}>
      <span className="verdict-diamond" aria-hidden="true">◆</span>
      <span>{text}</span>
    </p>
  );
}

export function Field({
  label,
  children,
  hint,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="field">
      <span className="field-label">{label}</span>
      {children}
      {hint ? <span className="field-hint">{hint}</span> : null}
    </label>
  );
}

export function DemoBadge() {
  return (
    <Link to="/hinweise" className="demo-badge" title="Erklärt Demo-Modus und offene Anschlüsse">
      Demo-Modus: erfundene Testdaten
    </Link>
  );
}

/** Sekundärer Button für Links innerhalb von Karten */
export function LinkButton({ onClick, children }: { onClick?: () => void; children: ReactNode }) {
  return (
    <button type="button" className="link-btn" onClick={onClick}>
      {children}
    </button>
  );
}

// Re-Exports für einfacheren Import
export { ToastProvider, useToast } from './ToastContext';
export { ConfirmDialog } from './ConfirmDialog';
export { Skeleton, SkeletonText, SkeletonBlock, SkeletonCard } from './Skeleton';
export { AdminTabs } from './AdminTabs';
export { Breadcrumbs } from './Breadcrumbs';
export type { SortConfig } from '../utils/sort';
export { getSortIcon, sortData } from '../utils/sort';
