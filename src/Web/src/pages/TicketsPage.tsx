// E4: Ticketliste (GET /tickets) mit Filtern nach Status, Schweregrad und
// „Meine Tickets“ sowie Seitenwahl (Paging beginnt bei 1). Neue Tickets per POST /tickets.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { EmptyState, ErrorBox, Field, SeverityBadge, Spinner, StatusBadge } from '../components/ui';
import { dueLabel } from '../utils';
import type { PagedDto, ProjectDto, Severity, TicketDto, TicketStatus } from '../types';

const PAGE_SIZE = 10;

export function TicketsPage() {
  const { token } = useAuth();

  const [status, setStatus] = useState<'' | TicketStatus>('');
  const [severity, setSeverity] = useState<'' | Severity>('');
  const [mine, setMine] = useState(false);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedDto<TicketDto> | null>(null);
  const [error, setError] = useState<unknown>(null);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    scanApi
      .listTickets(token, {
        status: status || undefined,
        severity: severity || undefined,
        assignedToMe: mine || undefined,
        page,
        pageSize: PAGE_SIZE,
      })
      .then(setResult)
      .catch(setError);
  }, [token, status, severity, mine, page]);

  useEffect(reload, [reload]);

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  return (
    <div>
      <h1>Tickets</h1>

      <div className="filters">
        <Field label="Status">
          <select
            value={status}
            onChange={(e) => {
              setPage(1);
              setStatus(e.target.value as '' | TicketStatus);
            }}
          >
            <option value="">alle</option>
            <option value="Open">Offen</option>
            <option value="InProgress">In Bearbeitung</option>
            <option value="Escalated">Eskaliert</option>
            <option value="Closed">Geschlossen</option>
            <option value="RiskAccepted">Risiko akzeptiert</option>
          </select>
        </Field>
        <Field label="Schweregrad">
          <select
            value={severity}
            onChange={(e) => {
              setPage(1);
              setSeverity(e.target.value as '' | Severity);
            }}
          >
            <option value="">alle</option>
            <option value="Critical">Kritisch</option>
            <option value="High">Hoch</option>
            <option value="Medium">Mittel</option>
            <option value="Low">Niedrig</option>
          </select>
        </Field>
        <Field label="Sicht">
          <label>
            <input
              type="checkbox"
              checked={mine}
              onChange={(e) => {
                setPage(1);
                setMine(e.target.checked);
              }}
            />{' '}
            Nur meine Tickets (persönliche Übernahme)
          </label>
        </Field>
      </div>

      {error ? <ErrorBox error={error} /> : null}
      {!result && !error ? <Spinner label="Tickets werden geladen…" /> : null}
      {result && result.items.length === 0 ? (
        <EmptyState text="Keine Tickets passen zu den gewählten Filtern." />
      ) : null}
      {result && result.items.length > 0 ? (
        <>
          <div className="card">
            <table>
              <thead>
                <tr>
                  <th>Titel</th>
                  <th>Schweregrad</th>
                  <th>Status</th>
                  <th>Stufe</th>
                  <th>Zuständige Rolle</th>
                  <th>Frist</th>
                  <th>Persönlicher Bearbeiter</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((t) => (
                  <tr key={t.id}>
                    <td>
                      <Link to={`/tickets/${t.id}`}>{t.title}</Link>
                    </td>
                    <td><SeverityBadge severity={t.severity} /></td>
                    <td><StatusBadge status={t.status} /></td>
                    <td>{t.escalationLevel}</td>
                    <td>{t.assignedRole ?? <span className="hint">keine</span>}</td>
                    <td>{dueLabel(t.dueAt)}</td>
                    <td>{t.assigneeName ?? <span className="hint">niemand übernommen</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pager">
            <button className="secondary" type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Zurück
            </button>
            <span>
              Seite {result.page} von {totalPages} · {result.totalCount} Tickets
            </span>
            <button
              className="secondary"
              type="button"
              disabled={result.page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Weiter
            </button>
          </div>
        </>
      ) : null}

      <NewTicketForm onCreated={() => reload()} />
    </div>
  );
}

function NewTicketForm({ onCreated }: { onCreated: () => void }) {
  const { token } = useAuth();
  const [open, setOpen] = useState(false);
  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [projectId, setProjectId] = useState('');
  const [severity, setSeverity] = useState<Severity>('Medium');
  const [cve, setCve] = useState('');
  const [system, setSystem] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    if (!token || !open) return;
    scanApi.listProjects(token).then(setProjects).catch(setError);
  }, [token, open]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!token) return;
    setBusy(true);
    setError(null);
    try {
      await scanApi.createTicket(token, {
        title: title.trim(),
        description: description.trim(),
        severity,
        cve: cve.trim() || null,
        system: system.trim() || null,
        projectId: projectId || null,
      });
      setTitle('');
      setDescription('');
      setCve('');
      setSystem('');
      setOpen(false);
      onCreated();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="card">
      <h2>
        <button type="button" className="link-btn" onClick={() => setOpen((o) => !o)}>
          {open ? 'Formular ausblenden' : 'Neuen Vorfall erfassen'}
        </button>
      </h2>
      {open ? (
        <form onSubmit={onSubmit}>
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
          <Field label="Projekt (optional)">
            <select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
              <option value="">– kein Projekt –</option>
              {projects.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </Field>
          <Field label="CVE (optional)" hint="Format CVE-JJJJ-NNNN, z. B. CVE-2021-44228">
            <input value={cve} onChange={(e) => setCve(e.target.value)} pattern="CVE-[0-9]{4}-[0-9]{4,}" maxLength={30} />
          </Field>
          <Field label="Betroffenes System (optional)">
            <input value={system} onChange={(e) => setSystem(e.target.value)} maxLength={200} />
          </Field>
          {error ? <ErrorBox error={error} /> : null}
          <div style={{ marginTop: 'var(--spacing-md)' }}>
            <button type="submit" disabled={busy || !title.trim() || !description.trim()}>
              {busy ? 'Wird angelegt…' : 'Vorfall erfassen'}
            </button>
          </div>
        </form>
      ) : null}
    </div>
  );
}
