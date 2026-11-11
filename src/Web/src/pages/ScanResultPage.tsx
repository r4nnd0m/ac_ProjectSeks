// E3: Ergebnisseite. GET /scans/{id} wird alle drei Sekunden abgefragt, bis
// Scanned oder Failed erreicht ist, die Seite verlassen wird oder fünf
// Minuten vergangen sind.

import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { describeError } from '../api/http';
import { EmptyState, ErrorBox, SeverityBadge, Spinner, StatusBadge, VerdictDisplay } from '../components/ui';
import { epssPercent, formatDateTime, severityOrder } from '../utils';
import { USE_MOCKS } from '../config';
import type { ScanDetailDto } from '../types';

const POLL_INTERVAL_MS = 3_000;
const POLL_TIMEOUT_MS = 5 * 60_000;

export function ScanResultPage() {
  const { id } = useParams<{ id: string }>();
  const { token } = useAuth();

  const [scan, setScan] = useState<ScanDetailDto | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [timedOut, setTimedOut] = useState(false);
  const [lastPoll, setLastPoll] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const stop = useCallback(() => {
    if (timerRef.current !== null) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  useEffect(() => {
    if (!token || !id) return;
    let stopped = false;
    const deadline = Date.now() + POLL_TIMEOUT_MS;

    const poll = async () => {
      if (stopped) return;
      try {
        const fresh = await scanApi.getScan(token, id);
        if (stopped) return;
        setScan(fresh);
        setLastPoll(new Date().toISOString());
        setError(null);
        if (fresh.status === 'Scanned' || fresh.status === 'Failed') {
          stop();
          return;
        }
      } catch (err) {
        if (stopped) return;
        setError(err);
      }
      if (Date.now() + POLL_INTERVAL_MS > deadline) {
        if (!stopped) setTimedOut(true);
        return;
      }
      timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
    };

    poll();
    return () => {
      stopped = true;
      stop();
    };
  }, [token, id, stop]);

  const findings = scan ? [...scan.findings].sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity]) : [];

  return (
    <div>
      <h1>Scan {scan ? scan.version : id}</h1>
      {scan ? (
        <p className="hint">
          <Link to={`/projects/${scan.projectId}`}>Zum Projekt</Link> · Erstellt{' '}
          {formatDateTime(scan.createdAt)} · Beendet {formatDateTime(scan.finishedAt)}
        </p>
      ) : null}

      {error && !scan ? <ErrorBox error={error} /> : null}
      {error && scan ? (
        <div className="errorbox" role="alert">
          Die letzte Aktualisierung ist fehlgeschlagen: {describeError(error)}
        </div>
      ) : null}
      {!scan && !error ? <Spinner label="Scan wird geladen…" /> : null}

      {scan ? (
        <>
          <dl className="kv">
            <dt>Status</dt>
            <dd><StatusBadge status={scan.status} /></dd>
            <dt>Urteil</dt>
            <dd><VerdictDisplay verdict={scan.verdict} status={scan.status} /></dd>
            {scan.errorMessage ? (
              <>
                <dt>Fehlerhinweis</dt>
                <dd className="hint">{scan.errorMessage}</dd>
              </>
            ) : null}
          </dl>

          {scan.status === 'Pending' || scan.status === 'Scanning' ? (
            <p className="hint" role="status">
              <span className="spinner" aria-hidden="true" />
              Die Seite fragt den Status alle 3 Sekunden ab
              {lastPoll ? ` · zuletzt ${formatDateTime(lastPoll)}` : ''}
              {USE_MOCKS ? ' · im Demo-Modus dauert ein Scan rund 4 Sekunden' : ''}.
            </p>
          ) : null}
          {timedOut ? (
            <div className="errorbox" role="alert">
              Die Abfrage wurde nach fünf Minuten beendet. Der Scan ist immer noch nicht fertig.
              Bitte später über „Neu prüfen“ erneut nachsehen.
            </div>
          ) : null}
          {(scan.status === 'Pending' || scan.status === 'Scanning') && !timedOut ? null : (
            <p>
              <button
                className="secondary"
                type="button"
                onClick={() => {
                  setTimedOut(false);
                  if (token && id) {
                    scanApi.getScan(token, id).then(setScan).catch(setError);
                  }
                }}
              >
                Neu prüfen
              </button>
            </p>
          )}

          <h2>Funde nach Schweregrad</h2>
          {findings.length === 0 ? (
            <EmptyState
              text={
                scan.status === 'Scanned'
                  ? 'Keine Funde – die Stückliste war unauffällig.'
                  : 'Noch keine Funde vorhanden.'
              }
            />
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Schweregrad</th>
                  <th>Komponente</th>
                  <th>Version</th>
                  <th>CVE / Schwachstellen-ID</th>
                  <th>KEV</th>
                  <th>EPSS</th>
                </tr>
              </thead>
              <tbody>
                {findings.map((f) => (
                  <tr key={f.id}>
                    <td><SeverityBadge severity={f.severity} /></td>
                    <td>{f.component}</td>
                    <td>{f.componentVersion}</td>
                    <td>
                      {f.cve ? <code>{f.cve}</code> : null}{' '}
                      {f.cve ? <span className="hint">/ {f.vulnId}</span> : <code>{f.vulnId}</code>}
                    </td>
                    <td>{f.isKev ? <span className="badge sev-Critical">KEV</span> : <span className="hint">nein</span>}</td>
                    <td>{epssPercent(f.epss)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      ) : null}
    </div>
  );
}
