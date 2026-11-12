// E4: Ticketdetail mit allen Feldern von TicketResponse. Aktionen:
// Übernehmen (POST /tickets/{id}/take), Schließen (POST /tickets/{id}/close),
// Bearbeiten (PUT /tickets/{id}). Nur für Admins: „Risiko akzeptieren“ mit
// Pflicht-Begründung ab 20 Zeichen (POST /tickets/{id}/accept-risk).
// Antwortet der Server dort mit 403, fehlt die MFA-Anmeldung.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { ApiError } from '../api/http';
import { ErrorBox, Field, SeverityBadge, Spinner, StatusBadge } from '../components/ui';
import { dueLabel, formatDateTime } from '../utils';
import type { Severity, TicketDto } from '../types';

const MIN_REASON = 20;
const MAX_REASON = 2000;

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { token, user } = useAuth();

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [actionError, setActionError] = useState<unknown>(null);
  const [riskError, setRiskError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [severity, setSeverity] = useState<Severity>('Medium');
  const [reason, setReason] = useState('');

  const apply = useCallback((t: TicketDto) => {
    setTicket(t);
    setTitle(t.title);
    setDescription(t.description);
    setSeverity(t.severity);
  }, []);

  const reload = useCallback(() => {
    if (!token || !id) return Promise.resolve();
    setError(null);
    return scanApi.getTicket(token, id).then(apply).catch(setError);
  }, [token, id, apply]);

  useEffect(() => {
    void reload();
  }, [reload]);

  /** Führt eine Aktion aus und lädt das Ticket danach neu (take/close/accept-risk liefern 204). */
  async function run(action: () => Promise<unknown>, onError: (e: unknown) => void = setActionError): Promise<boolean> {
    setBusy(true);
    setActionError(null);
    setRiskError(null);
    try {
      await action();
      await reload();
      return true;
    } catch (err) {
      onError(err);
      return false;
    } finally {
      setBusy(false);
    }
  }

  if (error) return <ErrorBox error={error} />;
  if (!ticket || !token) return <Spinner label="Ticket wird geladen…" />;

  const finished = ticket.status === 'Closed' || ticket.status === 'RiskAccepted';
  const reasonLength = reason.trim().length;
  const t = ticket;

  async function onSave(e: FormEvent) {
    e.preventDefault();
    const ok = await run(() =>
      scanApi.updateTicket(token!, t.id, { title: title.trim(), description: description.trim(), severity }),
    );
    if (ok) setEditing(false);
  }

  async function onAcceptRisk(e: FormEvent) {
    e.preventDefault();
    const ok = await run(() => scanApi.acceptRisk(token!, t.id, reason.trim()), setRiskError);
    if (ok) setReason('');
  }

  return (
    <div>
      <h1>{t.title}</h1>
      <p className="hint">
        <Link to="/tickets">Zur Ticketliste</Link> · Ticket <code>{t.id}</code>
      </p>
      {actionError ? <ErrorBox error={actionError} /> : null}

      <div className="card">
        <h2>Details</h2>
        <dl className="kv">
          <dt>Status</dt>
          <dd><StatusBadge status={t.status} /></dd>
          <dt>Schweregrad</dt>
          <dd><SeverityBadge severity={t.severity} /></dd>
          <dt>Eskalationsstufe</dt>
          <dd>{t.escalationLevel} von 3 (der Worker eskaliert im Hintergrund)</dd>
          <dt>Frist</dt>
          <dd>{dueLabel(t.dueAt)}</dd>
          <dt>Zuständige Rolle</dt>
          <dd>{t.assignedRole ?? 'keine'}</dd>
          <dt>Bearbeitet von</dt>
          <dd>{t.assigneeName ?? 'noch niemand übernommen'}</dd>
          <dt>Gemeldet von</dt>
          <dd>{t.reporterName}</dd>
          <dt>CVE</dt>
          <dd>{t.cve ? <code>{t.cve}</code> : '–'}</dd>
          <dt>System</dt>
          <dd>{t.system ?? '–'}</dd>
          <dt>Projekt</dt>
          <dd>{t.projectId ? <Link to={`/projects/${t.projectId}`}>{t.projectId}</Link> : '–'}</dd>
          <dt>Angelegt</dt>
          <dd>{formatDateTime(t.createdAt)}</dd>
          <dt>Abgeschlossen</dt>
          <dd>{formatDateTime(t.closedAt)}</dd>
        </dl>
        <h3>Beschreibung</h3>
        {/* Reiner Text, nie HTML */}
        <p className="pre">{t.description || '–'}</p>
      </div>

      <div className="card">
        <h2>Aktionen</h2>
        {finished ? <p className="hint">Das Ticket ist abgeschlossen.</p> : null}
        {!finished ? (
          <div style={{ display: 'flex', gap: 'var(--spacing-sm)', flexWrap: 'wrap', alignItems: 'center' }}>
            <button type="button" disabled={busy} onClick={() => run(() => scanApi.takeTicket(token, t.id))}>
              Übernehmen
            </button>
            <button type="button" className="secondary" disabled={busy} onClick={() => run(() => scanApi.closeTicket(token, t.id))}>
              Schließen
            </button>
            <button type="button" className="secondary" disabled={busy} onClick={() => setEditing((v) => !v)}>
              {editing ? 'Bearbeiten abbrechen' : 'Bearbeiten'}
            </button>
          </div>
        ) : null}
        {editing && !finished ? (
          <form onSubmit={onSave}>
            <Field label="Titel">
              <input value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={200} />
            </Field>
            <Field label="Beschreibung">
              <textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)} required maxLength={4000} />
            </Field>
            <Field label="Schweregrad">
              <select value={severity} onChange={(e) => setSeverity(e.target.value as Severity)}>
                <option value="Critical">Kritisch</option>
                <option value="High">Hoch</option>
                <option value="Medium">Mittel</option>
                <option value="Low">Niedrig</option>
              </select>
            </Field>
            <div style={{ marginTop: 'var(--spacing-md)' }}>
              <button type="submit" disabled={busy || !title.trim() || !description.trim()}>
                Änderungen speichern
              </button>
            </div>
          </form>
        ) : null}
      </div>

      {user?.role === 'Admin' && !finished ? (
        <div className="card">
          <h2>Risiko akzeptieren</h2>
          {!user.mfaVerified ? (
            <p className="hint">Hinweis: Du bist ohne zweiten Faktor angemeldet. Der Server lehnt die Freigabe dann ab.</p>
          ) : null}
          <form onSubmit={onAcceptRisk}>
            <Field label="Begründung" hint={`Pflichtfeld, mindestens ${MIN_REASON} Zeichen.`}>
              <textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} required maxLength={MAX_REASON} />
            </Field>
            {riskError instanceof ApiError && riskError.status === 403 ? (
              <div className="errorbox" role="alert">Dafür ist ein Login mit MFA nötig.</div>
            ) : riskError ? (
              <ErrorBox error={riskError} />
            ) : null}
            <button type="submit" className="danger" disabled={busy || reasonLength < MIN_REASON}>
              Risiko akzeptieren ({reasonLength}/{MIN_REASON} Zeichen)
            </button>
          </form>
        </div>
      ) : null}
    </div>
  );
}
