// E1/E5: Hinweisseite. Kennzeichnet den Demo-Modus und listet die
// angebundenen Endpunkte samt API.

import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { USE_MOCKS, simsConfig } from '../config';
import { DEMO_MFA_CODE, DEMO_PASSWORD, resetDemoData } from '../api/mock/db';

const ENDPUNKTE: Array<{ api: string; bereich: string; endpunkte: string }> = [
  { api: 'Auth', bereich: 'Anmeldung', endpunkte: 'POST /auth/login · POST /auth/mfa/verify · GET /auth/me · POST /auth/logout' },
  { api: 'Auth', bereich: 'Zwei-Faktor', endpunkte: 'POST /auth/mfa/setup · POST /auth/mfa/confirm' },
  { api: 'Auth', bereich: 'Verwaltung', endpunkte: 'GET/POST /users · POST /users/{id}/deactivate · GET/POST /teams' },
  { api: 'Scan', bereich: 'Projekte', endpunkte: 'GET/POST /projects · GET /projects/{id} · GET /projects/{id}/scans' },
  { api: 'Scan', bereich: 'Scans', endpunkte: 'POST /projects/{id}/scans?version=… · GET /scans/{id}' },
  { api: 'Scan', bereich: 'Tickets', endpunkte: 'GET/POST /tickets · GET/PUT /tickets/{id} · POST /tickets/{id}/take | close | accept-risk' },
  { api: 'Scan', bereich: 'Audit', endpunkte: 'GET /audit' },
  { api: 'News', bereich: 'Meldungen', endpunkte: 'GET /news' },
];

export function HinweisePage() {
  const navigate = useNavigate();

  const handleResetDemoData = useCallback(() => {
    resetDemoData();
  }, []);

  return (
    <div>
      <main className="content">
        <div className="public-content">
          <h1>Hinweise</h1>
          <p className="hint">Infos zum Demo-Modus und zu den angebundenen Schnittstellen.</p>

          <div className="public-actions">
            <button type="button" className="secondary" onClick={() => navigate('/projects')}>
              ← Zur Anwendung
            </button>
            {USE_MOCKS && (
              <button type="button" className="secondary" onClick={handleResetDemoData}>
                Demo-Daten zurücksetzen
              </button>
            )}
          </div>

          {USE_MOCKS ? (
            <div className="card demo-card">
              <div className="demo-strip"></div>
              <h2>Demo-Modus ist AKTIV</h2>
              <p>
                Die Oberfläche läuft gegen <strong>erfundene Testdaten</strong> im Browser
                (<code>VITE_USE_MOCKS</code> nicht auf <code>false</code>). Es werden keine Backends erreicht.
                Ein Scan mit <code>fail</code> in der Versionsangabe endet absichtlich als fehlgeschlagen.
              </p>
              <p>
                Demo-Konten (Passwort <code>{DEMO_PASSWORD}</code>): admin@sims.example, lead@sims.example,
                dev@sims.example, dev2@sims.example. Einmalcode für 2FA: <code>{DEMO_MFA_CODE}</code>.
              </p>
              <p>
                <button type="button" className="secondary" onClick={handleResetDemoData}>
                  Demo-Daten zurücksetzen
                </button>
              </p>
            </div>
          ) : (
            <div className="card">
              <h2>Demo-Modus ist AUS</h2>
              <p>
                Die Oberfläche spricht die echten APIs an: Auth <code>{simsConfig.authUrl}</code>, Scan{' '}
                <code>{simsConfig.scanUrl}</code>, News <code>{simsConfig.newsUrl}</code>.
              </p>
            </div>
          )}

          <div className="card">
            <h2>Angebundene Endpunkte</h2>
            <table>
              <thead>
                <tr>
                  <th>API</th>
                  <th>Bereich</th>
                  <th>Endpunkte</th>
                </tr>
              </thead>
              <tbody>
                {ENDPUNKTE.map((row) => (
                  <tr key={row.api + row.bereich}>
                    <td><span className="badge api-badge">{row.api}</span></td>
                    <td>{row.bereich}</td>
                    <td><code>{row.endpunkte}</code></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="hint">
              Rollen werden in der Oberfläche nur für die Anzeige genutzt. Die Rechte prüft immer der Server.
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
