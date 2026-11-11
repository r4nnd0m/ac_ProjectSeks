// E4: Audit-Protokoll (GET /audit, paged, nur Admins). Nachweis der
// Worker-Eskalationen und der Ticketaktionen (C5).

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { AdminTabs } from '../components/AdminTabs';
import { EmptyState, ErrorBox, Spinner } from '../components/ui';
import { formatDateTime } from '../utils';
import type { AuditEntryDto, PagedDto } from '../types';

const PAGE_SIZE = 20;

export function AdminAuditPage() {
  const { token } = useAuth();
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedDto<AuditEntryDto> | null>(null);
  const [error, setError] = useState<unknown>(null);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    scanApi.listAudit(token, page, PAGE_SIZE).then(setResult).catch(setError);
  }, [token, page]);

  useEffect(reload, [reload]);

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  return (
    <div>
      <h1>Verwaltung</h1>
      <AdminTabs />
      {error ? <ErrorBox error={error} /> : null}
      {!result && !error ? <Spinner label="Audit-Protokoll wird geladen…" /> : null}
      {result && result.items.length === 0 ? <EmptyState text="Noch keine Nachweise vorhanden." /> : null}
      {result && result.items.length > 0 ? (
        <>
          <table>
            <thead>
              <tr>
                <th>Zeitpunkt</th>
                <th>Wer</th>
                <th>Aktion</th>
                <th>Ticket</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((a) => (
                <tr key={a.id}>
                  <td>{formatDateTime(a.timestamp)}</td>
                  <td>{a.userName}</td>
                  <td><code>{a.action}</code></td>
                  <td>{a.ticketId ? <Link to={`/tickets/${a.ticketId}`}>Ticket öffnen</Link> : <span className="hint">–</span>}</td>
                  <td>{a.details ?? ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pager">
            <button className="secondary" type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Zurück
            </button>
            <span>Seite {result.page} von {totalPages} · {result.totalCount} Einträge</span>
            <button className="secondary" type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
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
