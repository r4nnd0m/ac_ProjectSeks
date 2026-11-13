// E4: Audit-Protokoll (GET /audit, paged, nur Admins). Nachweis der
// Worker-Eskalationen und der Ticketaktionen (C5).

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { Breadcrumbs, EmptyState, ErrorBox, Field, Spinner } from '../components/ui';
import { formatDateTime } from '../utils';
import type { AuditEntryDto } from '../types';

const PAGE_SIZE = 20;

export function AdminAuditPage() {
  const { token } = useAuth();

  const [page, setPage] = useState(1);
  const [allAudit, setAllAudit] = useState<AuditEntryDto[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [userFilter, setUserFilter] = useState('');
  const [actionFilter, setActionFilter] = useState('');

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    scanApi.listAudit(token, 1, 1000).then((res) => {
      setAllAudit(res.items);
    }).catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  // Client-seitiges Filtern
  const filteredAudit = useMemo(() => {
    let result = allAudit;

    if (userFilter) {
      result = result.filter((a) => a.userName.toLowerCase().includes(userFilter.toLowerCase()));
    }

    if (actionFilter) {
      result = result.filter((a) => a.action.toLowerCase().includes(actionFilter.toLowerCase()));
    }

    return result;
  }, [allAudit, userFilter, actionFilter]);

  // Pagination
  const paginatedAudit = filteredAudit.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const totalPages = Math.max(1, Math.ceil(filteredAudit.length / PAGE_SIZE));

  return (
    <div>
      <Breadcrumbs
        items={[
          { label: 'Verwaltung', to: '/admin/users' },
          { label: 'Audit-Protokoll', to: '/admin/audit' },
        ]}
      />
      <h1>Audit-Protokoll</h1>
      {error ? <ErrorBox error={error} /> : null}
      {!allAudit.length && !error ? <Spinner label="Audit-Protokoll wird geladen…" /> : null}
      {allAudit.length === 0 ? <EmptyState text="Noch keine Nachweise vorhanden." /> : null}

      <div className="filter-bar">
        <div className="filter-group">
          <Field label="Suche nach Aktion">
            <input
              type="text"
              value={actionFilter}
              onChange={(e) => setActionFilter(e.target.value)}
              placeholder="Aktion (z. B. take, close, accept-risk)..."
              className="search-field"
            />
          </Field>
        </div>
        <div className="filter-group">
          <Field label="Von Benutzer">
            <input
              type="text"
              value={userFilter}
              onChange={(e) => setUserFilter(e.target.value)}
              placeholder="Benutzername..."
            />
          </Field>
        </div>
      </div>

      {allAudit.length > 0 ? (
        <>
          <div className="card">
            <h2>Verlauf</h2>
            <div className="audit-timeline">
              {paginatedAudit.map((a) => (
                <div key={a.id} className="audit-entry">
                  <div className="audit-entry-content">
                    <div className="audit-entry-time">{formatDateTime(a.timestamp)}</div>
                    <div className="audit-entry-user">{a.userName}</div>
                    <div className="audit-entry-action">{a.action}</div>
                    {a.ticketId && (
                      <div style={{ marginTop: 'var(--spacing-xs)' }}>
                        Ticket:{' '}
                        <Link to={`/tickets/${a.ticketId}`}>#{a.ticketId}</Link>
                      </div>
                    )}
                    {a.details && <div className="audit-entry-details">{a.details}</div>}
                  </div>
                </div>
              ))}
            </div>
          </div>
          <div className="pager">
            <button className="secondary" type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Zurück
            </button>
            <span>
              Seite {page} von {totalPages} · {filteredAudit.length} Einträge
            </span>
            <button
              className="secondary"
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Weiter
            </button>
          </div>
        </>
      ) : null}
      <p>
        <button type="button" className="secondary" onClick={reload}>Neu laden</button>
      </p>
    </div>
  );
}
