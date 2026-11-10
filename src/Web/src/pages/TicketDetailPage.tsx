// E4: Ticket öffnen, bearbeiten, übernehmen, schließen. Eskalationsstufe,
// Frist, zuständige Rolle und persönliche Bearbeitung werden getrennt
// gezeigt (C1/C2). Risikoakzeptanz: nur Admin mit zweitem Faktor und mit
// Begründung ab 20 Zeichen. Die Eskalation selbst erledigt der Worker im
// Hintergrund – die Oberfläche zeigt nur ihr Ergebnis.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { ErrorBox, Field, SeverityBadge, Spinner, StatusBadge } from '../components/ui';
import { dueLabel, formatDateTime } from '../utils';
import type { TicketDetailDto } from '../types';

const MIN_REASON = 20;

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { token, user } = useAuth();

  const [ticket, setTicket] = useState<TicketDetailDto | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [actionError, setActionError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');

  const [reason, setReason] = useState('');

  const reload = useCallback(() => {
    if (!token || !id) return;
    setError(null);
    scanApi
      .getTicket(token, id)
      .then((t) => {
        setTicket(t);
        setTitle(t.title);
        setDescription(t.description ?? '');
      })
      .catch(setError);
  }, [token, id]);

  useEffect(reload, [reload]);

  async function run(action: () => Promise<TicketDetailDto>) {
    setBusy(true);
    setActionError(null);
    try {
      setTicket(await action());
    } catch (err) {
      setActionError(err);
    } finally {
      setBusy(false);
    }
  }

  if (error) return <ErrorBox error={error} />;
  if (!ticket) return <Spinner label="Ticket wird geladen…" />;

  const finished = ticket.status === 'Closed' || ticket.status === 'RiskAccepted';

  return (
    <div>
      <h1>{ticket.title}</h1>
      <p className="hint">
        Ticket {ticket.id} · <Link to={`/projects/${ticket.projectId}`}>Projekt {ticket.projectId}</Link> ·
        Angelegt {formatDateTime(ticket.createdAt)} · Zuletzt geändert {formatDateTime(ticket.updatedAt)}
      </p>
      {actionError ? <ErrorBox error={actionError} /> : null}

      <div className="card">
        <h2>Status und Eskalation</h2>
        <dl className="kv">
          <dt>Status</dt>
          <dd><StatusBadge status={ticket.status} /></dd>
          <dt>Schweregrad</dt>
          <dd><SeverityBadge severity={ticket.severity} /></dd>
          <dt>Eskalationsstufe</dt>
          <dd>{ticket.escalationLevel} von 3 (eskaliert der Worker im Hintergrund)</dd>
          <dt>Frist</dt>
          <dd>{dueLabel(ticket.dueAt)}</dd>
          <dt>Zuständige Rolle</dt>
          <dd>
            {ticket.assignedRole ?? 'keine (regulär, Stufe 1)'}{' '}
            <span className="hint">— die Rolle am Ticket, unabhängig von Benutzerkonten</span>
          </dd>
          <dt>Persönliche Bearbeitung</dt>
          <dd>
            {ticket.assigneeName ?? 'bisher hat niemand dieses Ticket übernommen'}
          </dd>
          {ticket.scanId ? (
            <>
              <dt>Herkunft</dt>
              <dd>
                <Link to={`/scans/${ticket.scanId}`}>Aus Scan {ticket.scanId}</Link>
                {ticket.findingId ? ` · Fund ${ticket.findingId}` : ''}
              </dd>
            </>
          ) : null}
        </dl>
        {ticket.description ? <p>{ticket.description}</p> : <p className="hint">Keine Beschreibung.</p>}
      </div>

      <div className="card">
        <h2>Aktionen</h2>
        {!finished && ticket.status !== 'InProgress' ? (
          <button type="button" disabled={busy} onClick={() => run(() => scanApi.takeTicket(token!, ticket.id))}>
            Ticket übernehmen
          </button>
        ) : null}{' '}
        {!finished ? (
          <button type="button" className="secondary" disabled={busy} onClick={() => run(() => scanApi.closeTicket(token!, ticket.id))}>
            Schließen
          </button>
        ) : null}{' '}
        <button type="button" className="secondary" disabled={busy} onClick={() => setEditing((e) => !e)}>
          {editing ? 'Bearbeiten abbrechen' : 'Bearbeiten'}
        </button>
        {editing ? (
          <form
            onSubmit={(e: FormEvent) => {
              e.preventDefault();
              run(() => scanApi.updateTicket(token!, ticket.id, { title: title.trim(), description: description.trim() }))
                .then(() => setEditing(false));
            }}
          >
            <Field label="Titel">
              <input value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={150} />
            </Field>
            <Field label="Beschreibung">
              <textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)} />
            </Field>
            <button type="submit" disabled={busy || !title.trim()}>Änderungen speichern</button>
          </form>
        ) : null}
      </div>

      <div className="card">
        <h2>Risiko akzeptieren</h2>
        {!user?.mfaVerified ? (
          <p className="hint">
            Nur eine Person mit Admin-Rolle <em>und</em> bestätigtem zweitem Faktor darf ein Risiko akzeptieren.
            Deine aktuelle Anmeldung hat keinen bestätigten zweiten Faktor.
          </p>
        ) : user.role !== 'Admin' ? (
          <p className="hint">Nur Admins dürfen Risiken akzeptieren.</p>
        ) : finished ? (
          <p className="hint">Das Ticket ist bereits abgeschlossen.</p>
        ) : (
          <form
            onSubmit={(e: FormEvent) => {
              e.preventDefault();
              run(() => scanApi.acceptRisk(token!, ticket.id, reason).then((t) => { setReason(''); return t; }));
            }}
          >
            <Field
              label="Begründung"
              hint={`Mindestens ${MIN_REASON} Zeichen. Die Prüfung erfolgt zusätzlich im Backend.`}
            >
              <textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
            </Field>
            <button
              type="submit"
              className="danger"
              disabled={busy || reason.trim().length < MIN_REASON}
            >
              Risiko akzeptieren ({reason.trim().length}/{MIN_REASON} Zeichen)
            </button>
          </form>
        )}
      </div>

      <div className="card">
        <h2>Audit-Protokoll dieses Tickets</h2>
        {ticket.audit.length === 0 ? (
          <p className="hint">Noch keine Einträge.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Zeitpunkt</th>
                <th>Wer</th>
                <th>Aktion</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {[...ticket.audit].reverse().map((a) => (
                <tr key={a.id}>
                  <td>{formatDateTime(a.timestamp)}</td>
                  <td>{a.userName}</td>
                  <td><code>{a.action}</code></td>
                  <td>{a.details}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
