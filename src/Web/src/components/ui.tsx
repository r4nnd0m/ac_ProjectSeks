// Wiederverwendbare, schlichte Bausteine: Lade-, Leer-, Fehler- und
// Rechteanzeigen sowie Badge und Urteils-Raute.

import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import type { Severity, Verdict } from '../types';
import { describeError } from '../api/http';
import { severityLabels, statusLabels, verdictLabels } from '../utils';

export function Spinner({ label }: { label: string }) {
  return (
    <p className="hint" role="status">
      <span className="spinner" aria-hidden="true" /> {label}
    </p>
  );
}

export function EmptyState({ text }: { text: string }) {
  return <p className="hint empty">{text}</p>;
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
    <ErrorBox error={new Error('Dafür fehlen dir die Rechte. Die verbindliche Prüfung liegt im Backend.')}>
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
 * Urteil als Raute mit Farbe und Text (E3):
 * Go grün, Go mit Auflagen gelb, NoGo rot; laufende/fehlgeschlagene Scans
 * und fehlende Urteile grau.
 */
export function VerdictDisplay({ verdict, status }: { verdict: Verdict | null; status: string }) {
  const cls =
    verdict === 'Go' ? 'verdict-go'
      : verdict === 'GoWithConditions' ? 'verdict-gwc'
        : verdict === 'NoGo' ? 'verdict-nogo'
          : 'verdict-none';
  const text =
    verdict ? verdictLabels[verdict]
      : status === 'Pending' ? 'Wartet auf den Worker'
        : status === 'Scanning' ? 'Scan läuft – noch kein Urteil'
          : status === 'Failed' ? 'Fehlgeschlagen – kein Urteil'
            : 'Kein Urteil';
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
