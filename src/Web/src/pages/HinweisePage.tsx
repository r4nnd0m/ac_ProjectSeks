// E1/E5: Hinweisseite. Kennzeichnet den Demo-Modus und listet, welche
// Testdaten erfunden sind und welche Anschlüsse noch mit dem Backend
// vereinbart werden müssen.

import { Link } from 'react-router-dom';
import { USE_MOCKS, simsConfig } from '../config';
import { resetDemoData } from '../api/mock/db';
import { DEMO_MFA_CODE } from '../api/mock/db';

const OFFENE_ANSCHLUESSE: Array<{ bereich: string; endpunkt: string; grund: string }> = [
  {
    bereich: 'Auth',
    endpunkt: 'POST /auth/login · POST /auth/mfa/verify · GET /auth/me · POST /auth/mfa/setup · POST /auth/mfa/confirm',
    grund: 'PROVISORISCH: Die Auth-API hat noch keine Controller; die Pfadnamen sind von uns vorgeschlagen und müssen abgestimmt werden.',
  },
  {
    bereich: 'Projekte',
    endpunkt: 'GET /projects · POST /projects · GET /projects/{id} · GET /projects/{id}/scans',
    grund: 'PROVISORISCH: In B2/B3 nur der Upload-Pfad bestätigt; Liste/Anlesen noch nicht vereinbart.',
  },
  {
    bereich: 'Scans',
    endpunkt: 'POST /projects/{id}/scans?version=… · GET /scans/{id}',
    grund: 'Aus B2/B5 bekannt. Antwortform (HTTP 202 + scanId) und Feldnamen müssen final bestätigt werden.',
  },
  {
    bereich: 'Tickets',
    endpunkt: 'GET /tickets · GET /tickets/{id} · POST /tickets/{id}/take',
    grund: 'Aus C2 bekannt. Liste mit Filter und Seiten ist nur der Idee nach beschrieben.',
  },
  {
    bereich: 'Tickets',
    endpunkt: 'POST /tickets · PUT /tickets/{id} · POST /tickets/{id}/close · POST /tickets/{id}/accept-risk',
    grund: 'PROVISORISCH: Anlegen, Bearbeiten, Schließen und Risikoakzeptanz sind in C1–C5 nicht als Route festgelegt.',
  },
  {
    bereich: 'Verwaltung',
    endpunkt: 'GET/POST /users · POST /users/{id}/disable · GET/POST /teams · GET /audit',
    grund: 'PROVISORISCH: C5 beschreibt nur den AuditService im Backend; die Lesewege für die Oberfläche fehlen.',
  },
  {
    bereich: 'News',
    endpunkt: 'GET /news',
    grund: 'Aus dem Auftrag bekannt; Feldnamen (source, publishedAt, url) noch nicht OpenAPI-fest.',
  },
];

export function HinweisePage() {
  return (
    <div>
      <h1>Hinweise: Demo-Modus und offene Anschlüsse</h1>

      <div className="card">
        <h2>Demo-Modus {USE_MOCKS ? 'ist AKTIV' : 'ist AUS'}</h2>
        {USE_MOCKS ? (
          <>
            <p>
              Diese Oberfläche läuft gerade gegen <strong>erfundene Testdaten</strong> im Browser
              (<code>VITE_USE_MOCKS=true</code>, voreingestellt, wenn die Variable nicht gesetzt ist).
              Es werden keine echten Backends erreicht.
            </p>
            <p>
              Erfunden sind unter anderem: alle Projekte, Scans, Funde, Urteile (Go / Go mit Auflagen /
              NoGo), Tickets samt Eskalationsstufen, Fristen und Rollen, Benutzer, Teams, das
              Audit-Protokoll und die News. Auch die „Scans“ im Demo-Modus sind nachgebaut: Nach dem
              Upload vergehen ein paar Sekunden, dann erscheinen ein erdachtes Urteil und ein
              automatisches Ticket. Ein Scan mit <code>fail</code> in der Versionsangabe endet
              fehlgeschlagen – das zeigt den zweiten geforderten Beispielablauf.
            </p>
            <p>
              Angemeldet wird gegen die Demo-Konten (Passwort jeweils <code>demo1234</code>):{' '}
              admin@sims.example, lead@sims.example, dev@sims.example, dev2@sims.example. Der
              Einmalcode der Zwei-Faktor-Anmeldung ist im Demo-Modus immer <code>{DEMO_MFA_CODE}</code>.
            </p>
            <p>
              <button type="button" className="secondary" onClick={() => resetDemoData()}>
                Demo-Daten zurücksetzen
              </button>
            </p>
          </>
        ) : (
          <p>
            Ohne Demo-Modus spricht die Oberfläche die echten APIs an: Auth{' '}
            <code>{simsConfig.authUrl}</code>, Scan <code>{simsConfig.scanUrl}</code>, News{' '}
            <code>{simsConfig.newsUrl}</code> (Basis-URLs in <code>public/config.js</code>).
          </p>
        )}
      </div>

      <div className="card">
        <h2>Offene Anschlüsse (was das Backend noch liefern muss)</h2>
        <p className="hint">
          Im Repo existieren noch keine OpenAPI-Beschreibungen; die drei APIs sind Gerüste ohne
          Controller. Alle Typen in <code>src/types.ts</code> sind deshalb provisorisch und aus den
          Abstimmungs-Dokumenten (<code>docs/worker/abstimmung/</code>) abgeleitet. Sobald OpenAPI-Dateien
          vorliegen, müssen sie gegen die Typen ausgetauscht werden.
        </p>
        <table>
          <thead>
            <tr>
              <th>Bereich</th>
              <th>Endpunkt(e)</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {OFFENE_ANSCHLUESSE.map((row) => (
              <tr key={row.bereich + row.endpunkt}>
                <td>{row.bereich}</td>
                <td><code>{row.endpunkt}</code></td>
                <td>{row.grund}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="hint">
          Verbindlich bleiben außerdem: die Rechteprüfung im Backend (Rollen aus{' '}
          <code>Sims.Contracts</code>), echte Scanurteile nach B4, echte Ticketregeln und Eskalationen
          nach C3/C4 sowie das gemeinsame Audit nach C5. Die Oberfläche zeigt diese Ergebnisse nur an.
        </p>
        <p>
          Weiter: <Link to="/news">Öffentliche Meldungen</Link> ·{' '}
          <Link to="/login">Anmeldung</Link>
        </p>
      </div>
    </div>
  );
}
