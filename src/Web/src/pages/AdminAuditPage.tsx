// E4: Audit-Protokoll einsehen (nur Admins). Nachweis der vom Worker
// durchgeführten Eskalationen und aller Oberflächenaktionen (C5).

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { AdminTabs } from '../components/AdminTabs';
import { EmptyState, ErrorBox, Spinner } from '../components/ui';
import { formatDateTime } from '../utils';
import type { AuditEntryDto } from '../types';

export function AdminAuditPage() {
  const { token } = useAuth();
  const [entries, setEntries] = useState<AuditEntryDto[] | null>(null);
  const [error, setError] = useState<unknown>(null);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    scanApi.listAudit(token).then(setEntries).catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  return (
    <div>
      <h1>Verwaltung</h1>
      <AdminTabs />
      {error ? <ErrorBox error={error} /> : null}
      {!entries && !error ? <Spinner label="Audit-Protokoll wird geladen…" /> : null}
      {entries && entries.length === 0 ? <EmptyState text="Noch keine Nachweise vorhanden." /> : null}
      {entries && entries.length > 0 ? (
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
            {entries.map((a) => (
              <tr key={a.id}>
                <td>{formatDateTime(a.timestamp)}</td>
                <td>{a.userName}</td>
                <td><code>{a.action}</code></td>
                <td>{a.ticketId ? <Link to={`/tickets/${a.ticketId}`}>{a.ticketId}</Link> : <span className="hint">–</span>}</td>
                <td>{a.details}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
      <p>
        <button type="button" className="secondary" onClick={reload}>Neu laden</button>
      </p>
    </div>
  );
}
