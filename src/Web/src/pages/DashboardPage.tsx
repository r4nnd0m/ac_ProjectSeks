// Dashboard-Startseite nach E2: Kennzahlen, neueste Tickets/Scans, Diagramm.

import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { Breadcrumbs, SeverityBadge, StatusBadge, Spinner } from '../components/ui';
import { formatDate } from '../utils';
import type { ProjectDto, ScanSummaryDto, TicketDto } from '../types';

const PAGE_SIZE = 1000;

export function DashboardPage() {
  const { token } = useAuth();
  const navigate = useNavigate();

  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [allTickets, setAllTickets] = useState<TicketDto[]>([]);
  const [allScans, setAllScans] = useState<ScanSummaryDto[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);

  // Daten laden
  const loadData = useCallback(async () => {
    if (!token) return;
    setError(null);
    setLoading(true);

    try {
      const [projectsData, ticketsRes] = await Promise.all([
        scanApi.listProjects(token),
        scanApi.listTickets(token, { page: 1, pageSize: PAGE_SIZE }),
      ]);

      setProjects(projectsData);
      setAllTickets(ticketsRes.items);

      // Scans sammeln (pro Projekt die neuesten)
      const scanPromises = projectsData.map((p) =>
        scanApi.listProjectScans(token, p.id).catch(() => [])
      );
      const scanArrays = await Promise.all(scanPromises);
      const scansFlat = scanArrays.flat().sort((a, b) =>
        b.createdAt.localeCompare(a.createdAt)
      );
      setAllScans(scansFlat);

      setLoading(false);
    } catch (err) {
      setError(err);
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Hilfsfunktionen für Zählungen
  const isOpenOrInProgress = (t: TicketDto) =>
    ['Open', 'InProgress', 'Escalated'].includes(t.status);

  const nonClosedTickets = allTickets.filter(isOpenOrInProgress);
  const criticalTickets = nonClosedTickets.filter((t) => t.severity === 'Critical');
  const openTickets = allTickets.filter((t) => t.status === 'Open');
  const escalatedTickets = allTickets.filter((t) => t.status === 'Escalated');

  //tickets nach Schweregrad (nur nicht abgeschlossene)
  const severityCounts = nonClosedTickets.reduce(
    (acc, t) => {
      acc[t.severity] = (acc[t.severity] || 0) + 1;
      return acc;
    },
    { Critical: 0, High: 0, Medium: 0, Low: 0 }
  );

  // Projekt-Farben (Team-orientiert für visuelle Unterscheidung)
  const getProjectColor = (projectId: string | null) => {
    // Demo-Projekte aus db.ts: p-shelf (team-blue), p-gateway (team-green)
    if (projectId === 'p-shelf') return '#0066cc'; // blau
    if (projectId === 'p-gateway') return '#228b22'; // grün
    return '#e0e0e0'; // fallback
  };

  const projectNames: Record<string, string> = {};
  projects.forEach((p) => (projectNames[p.id] = p.name));

  // Diagramm-Daten (max 100px height)
  const maxCount = Math.max(severityCounts.Critical, severityCounts.High, severityCounts.Medium, severityCounts.Low, 1);

  // Neueste Scans (bereits sortiert in loadData)
  const latestScans = allScans.slice(0, 5);

  // Neueste Tickets (bereits sortiert via listTickets mit pageSize 1000 → sortBy createdAt desc im Backend)
  const latestTickets = allTickets.filter(isOpenOrInProgress).slice(0, 5);

  return (
    <div>
      <Breadcrumbs items={[{ label: 'Dashboard' }]} />
      <h1>Dashboard</h1>
      {error ? (
        <div className="errorbox" role="alert">
          <strong>Fehler beim Laden:</strong> Bitte laden Sie die Seite neu.
        </div>
      ) : null}

      {loading ? (
        <Spinner label="Daten werden geladen…" />
      ) : (
        <>
          {/* Kacheln */}
          <div className="kpi-grid">
            {/* Offene Tickets */}
            <div className="card kpi-card">
              <div className="kpi-label">Offene Tickets</div>
              <div className="kpi-value">{openTickets.length}</div>
              <div className="hint">
                Status: offen · {escalatedTickets.length} eskaliert
              </div>
            </div>

            {/* Kritische Tickets */}
            <div className={`card kpi-card ${criticalTickets.length > 0 ? 'danger' : ''}`}>
              <div className="kpi-label">Kritische Tickets</div>
              <div className="kpi-value">{criticalTickets.length}</div>
              <div className="hint">
                {criticalTickets.length > 0 ? 'Aktion erforderlich' : 'Alles ruhig'}
              </div>
            </div>

            {/* Projekte */}
            <div className="card kpi-card">
              <div className="kpi-label">Projekte</div>
              <div className="kpi-value">{projects.length}</div>
              <div className="hint">
                {projects.length === 0 ? 'Noch keine Projekte' : 'Alle sichtbaren Projekte'}
              </div>
            </div>

            {/* Letzte Scans */}
            <div className="card kpi-card">
              <div className="kpi-label">Letzte Scans</div>
              <div className="kpi-value">{allScans.length}</div>
              <div className="hint">
                {allScans.length === 0
                  ? 'Noch keine Scans'
                  : `${allScans.filter((s) => s.status === 'Scanned' && s.verdict === 'Go').length} grün`}
              </div>
            </div>
          </div>

          {/* Diagramm: Tickets nach Schweregrad */}
          <div className="card" style={{ marginTop: 'var(--spacing-lg)' }}>
            <h2>Tickets nach Schweregrad (nicht abgeschlossen)</h2>
            <div className="chart-container">
              <div className="chart-bar-group">
                <div
                  className="chart-bar"
                  style={{
                    height: `${(severityCounts.Critical / maxCount) * 80}%`,
                    backgroundColor: 'var(--severity-critical)',
                  }}
                />
                <span className="chart-label">Kritisch</span>
                <span className="chart-value">{severityCounts.Critical}</span>
              </div>
              <div className="chart-bar-group">
                <div
                  className="chart-bar"
                  style={{
                    height: `${(severityCounts.High / maxCount) * 80}%`,
                    backgroundColor: 'var(--severity-high)',
                  }}
                />
                <span className="chart-label">Hoch</span>
                <span className="chart-value">{severityCounts.High}</span>
              </div>
              <div className="chart-bar-group">
                <div
                  className="chart-bar"
                  style={{
                    height: `${(severityCounts.Medium / maxCount) * 80}%`,
                    backgroundColor: 'var(--severity-medium)',
                  }}
                />
                <span className="chart-label">Mittel</span>
                <span className="chart-value">{severityCounts.Medium}</span>
              </div>
              <div className="chart-bar-group">
                <div
                  className="chart-bar"
                  style={{
                    height: `${(severityCounts.Low / maxCount) * 80}%`,
                    backgroundColor: 'var(--severity-low)',
                  }}
                />
                <span className="chart-label">Niedrig</span>
                <span className="chart-value">{severityCounts.Low}</span>
              </div>
            </div>
          </div>

          {/* Neueste Tickets */}
          <div className="card" style={{ marginTop: 'var(--spacing-lg)' }}>
            <h2>Neueste Tickets</h2>
            {latestTickets.length === 0 ? (
              <p className="hint">Keine nicht abgeschlossenen Tickets.</p>
            ) : (
              <div className="ticket-list">
                {latestTickets.map((t) => (
                  <div key={t.id} className="ticket-item">
                    <div className="ticket-header">
                      <h3>{t.title}</h3>
                      <div className="ticket-meta">
                        <SeverityBadge severity={t.severity} />
                        <StatusBadge status={t.status} />
                      </div>
                    </div>
                    <p className="hint">{formatDate(t.createdAt)}</p>
                  </div>
                ))}
              </div>
            )}
            {allTickets.length > 5 && (
              <button
                type="button"
                className="link-btn"
                style={{ marginTop: 'var(--spacing-sm)' }}
                onClick={() => navigate('/tickets')}
              >
                Alle Tickets anzeigen
              </button>
            )}
          </div>

          {/* Neueste Scans */}
          <div className="card" style={{ marginTop: 'var(--spacing-lg)' }}>
            <h2>Neueste Scans</h2>
            {allScans.length === 0 ? (
              <p className="hint">Noch keine Scans durchgeführt.</p>
            ) : (
              <div className="scan-list">
                {latestScans.map((s) => {
                  const projectName = projectNames[s.projectId] || 'Unbekanntes Projekt';
                  const projectColor = s.projectId ? getProjectColor(s.projectId) : '#e0e0e0';
                  return (
                    <div key={s.id} className="scan-item">
                      <div className="scan-header">
                        <div className="scan-project" style={{ borderLeftColor: projectColor }}>
                          <span className="scan-project-name">{projectName}</span>
                        </div>
                        <div className="scan-meta">
                          <span className="hint">{formatDate(s.createdAt)}</span>
                          <span className="badge scan-status">{s.status}</span>
                          {s.verdict && <span className={`badge verdict verdict-${s.verdict.toLowerCase()}`}>{s.verdict}</span>}
                        </div>
                      </div>
                      <p>
                        <code>{s.version}</code>
                      </p>
                    </div>
                  );
                })}
              </div>
            )}
            {allScans.length > 5 && (
              <button
                type="button"
                className="link-btn"
                style={{ marginTop: 'var(--spacing-sm)' }}
                onClick={() => navigate('/projects')}
              >
                Alle Scans anzeigen
              </button>
            )}
          </div>
        </>
      )}
    </div>
  );
}
