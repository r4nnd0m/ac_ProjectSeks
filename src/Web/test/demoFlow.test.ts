// Smoke-Test für den Demo-Durchlauf mit erfundenen Testdaten:
// anmelden, Projekt anlegen, Beispiel-SBOM hochladen, Urteil sehen,
// Ticket öffnen/bearbeiten/übernehmen/schließen, zweites Beispiel mit
// fehlgeschlagenem Scan, Risikoakzeptanz-Regeln.
//
// Er ersetzt keinen Klicktest im Browser, prüft aber die komplette
// Mock-Schicht inklusive Rechte- und Fristlogik.

import { beforeAll, describe, expect, it } from 'vitest';
import { authApi } from '../src/api/authApi';
import { scanApi } from '../src/api/scanApi';
import { newsApi } from '../src/api/newsApi';
import { ApiError } from '../src/api/http';
import { DEMO_MFA_CODE, resetDemoData } from '../src/api/mock/db';
import { validateSbom, EXAMPLE_SBOM } from '../src/pages/ProjectDetailPage';
import type { AuthSessionDto, ScanDetailDto } from '../src/types';

async function login(email: string, withMfa = true): Promise<AuthSessionDto> {
  const result = await authApi.login(email, 'demo1234');
  if (result.mfaRequired) {
    if (!withMfa) throw new Error('erwartete MFA-Pflicht');
    return authApi.verifyMfa(result.mfaToken, DEMO_MFA_CODE);
  }
  return result.session;
}

/** Wartet wie die Ergebnisseite, aber mit kürzerem Intervall für Tests. */
async function pollUntilDone(token: string, scanId: string): Promise<ScanDetailDto> {
  const deadline = Date.now() + 20_000;
  for (;;) {
    const scan = await scanApi.getScan(token, scanId);
    if (scan.status === 'Scanned' || scan.status === 'Failed') return scan;
    if (Date.now() > deadline) throw new Error(`Scan ${scanId} wurde nicht fertig`);
    await new Promise((r) => setTimeout(r, 120));
  }
}

beforeAll(() => {
  resetDemoData();
});

describe('Anmeldung (E2)', () => {
  it('lead-Anmeldung verlangt zweiten Faktor und funktioniert mit Einmalcode', async () => {
    const result = await authApi.login('lead@sims.example', 'demo1234');
    expect(result.mfaRequired).toBe(true);
    if (!result.mfaRequired) return;
    const session = await authApi.verifyMfa(result.mfaToken, DEMO_MFA_CODE);
    expect(session.user.role).toBe('Lead');
    expect(session.user.mfaVerified).toBe(true);
  });

  it('lehnt ein falsches Passwort ab', async () => {
    await expect(authApi.login('lead@sims.example', 'falsch')).rejects.toBeInstanceOf(ApiError);
  });

  it('Developer ohne 2FA bekommt sofort eine Sitzung', async () => {
    const session = await login('dev@sims.example', false);
    expect(session.user.role).toBe('Developer');
    expect(session.user.mfaVerified).toBe(false);
  });
});

describe('Projekte und Scans (E3)', () => {
  it('zeigt Developer nur Projekte des eigenen Teams, Admin alle', async () => {
    const dev = await login('dev@sims.example', false);
    const devProjects = await scanApi.listProjects(dev.token);
    expect(devProjects.length).toBeGreaterThan(0);
    expect(devProjects.every((p) => p.teamId === dev.user.teamId)).toBe(true);

    const admin = await login('admin@sims.example');
    const adminProjects = await scanApi.listProjects(admin.token);
    expect(adminProjects.length).toBeGreaterThan(devProjects.length);
  });

  it('prüft SBOM auf JSON, CycloneDX und Größe', () => {
    expect(validateSbom('kein json')).toMatch(/kein gültiges JSON/);
    expect(validateSbom('{"a":1}')).toMatch(/CycloneDX/);
    expect(validateSbom(EXAMPLE_SBOM)).toBeNull();
    expect(validateSbom('{"bomFormat":"CycloneDX","components":[],"x":"' + 'a'.repeat(6 * 1024 * 1024) + '"}')).toMatch(/5 MB/);
  });

  it('legt ein Projekt an, lädt eine Beispiel-SBOM hoch und sieht ein Urteil', async () => {
    const lead = await login('lead@sims.example');
    const project = await scanApi.createProject(lead.token, 'Kassen-Teststand');
    expect(project.name).toBe('Kassen-Teststand');
    expect(project.teamId).toBe(lead.user.teamId);

    const scan = await scanApi.createScan(lead.token, project.id, '1.0.0', EXAMPLE_SBOM);
    expect(scan.status).toBe('Pending');
    expect(scan.verdict).toBeNull();

    const done = await pollUntilDone(lead.token, scan.id);
    expect(done.status).toBe('Scanned');
    expect(done.verdict).toBe('GoWithConditions');
    expect(done.findings.length).toBeGreaterThan(0);

    const list = await scanApi.listProjectScans(lead.token, project.id);
    expect(list.some((s) => s.id === scan.id)).toBe(true);
  }, 25_000);

  it('zeigt den zweiten Beispielablauf: fehlgeschlagener Scan ohne Urteil', async () => {
    const lead = await login('lead@sims.example');
    const project = await scanApi.listProjects(lead.token).then((l) => l[0]);
    const scan = await scanApi.createScan(lead.token, project.id, 'fail-1.0.0', EXAMPLE_SBOM);
    const done = await pollUntilDone(lead.token, scan.id);
    expect(done.status).toBe('Failed');
    expect(done.verdict).toBeNull();
    expect(done.errorMessage).toBeTruthy();
  }, 25_000);

  it('verweigert teamfremden Zugriff auf ein Projekt', async () => {
    const devOther = await login('dev2@sims.example', false); // Team Grün
    await expect(scanApi.getProject(devOther.token, 'p-shelf')).rejects.toMatchObject({ status: 403 });
  });
});

describe('Tickets und Verwaltung (E4)', () => {
  it('zeigt Rolle und persönliche Bearbeitung getrennt und arbeitet ein Ticket durch', async () => {
    const lead = await login('lead@sims.example');
    const page = await scanApi.listTickets(lead.token, { page: 1, pageSize: 10 });
    expect(page.totalCount).toBeGreaterThan(0);

    const escalated = await scanApi.getTicket(lead.token, 'tk-2');
    expect(escalated.assignedRole).toBe('Lead');
    expect(escalated.assigneeName).toBeNull();
    expect(escalated.escalationLevel).toBe(2);

    // „Meine Tickets“ ist vor der Übernahme leer.
    const before = await scanApi.listTickets(lead.token, { mine: true });
    expect(before.items.some((t) => t.id === 'tk-2')).toBe(false);

    const taken = await scanApi.takeTicket(lead.token, 'tk-2');
    expect(taken.assigneeName).toBe(lead.user.name);
    expect(taken.status).toBe('InProgress');

    const after = await scanApi.listTickets(lead.token, { mine: true });
    expect(after.items.some((t) => t.id === 'tk-2')).toBe(true);

    const closed = await scanApi.closeTicket(lead.token, 'tk-2');
    expect(closed.status).toBe('Closed');
  });

  it('filtert nach Status und Schweregrad', async () => {
    const lead = await login('lead@sims.example');
    const closed = await scanApi.listTickets(lead.token, { status: 'Closed' });
    expect(closed.items.every((t) => t.status === 'Closed')).toBe(true);
    const critical = await scanApi.listTickets(lead.token, { severity: 'Critical' });
    expect(critical.items.every((t) => t.severity === 'Critical')).toBe(true);
  });

  it('lässt nur einen mit 2FA angemeldeten Admin ein Risiko mit Begründung ab 20 Zeichen zu', async () => {
    const dev = await login('dev@sims.example', false);
    await expect(scanApi.acceptRisk(dev.token, 'tk-3', 'x'.repeat(25))).rejects.toMatchObject({ status: 403 });

    const admin = await login('admin@sims.example');
    await expect(scanApi.acceptRisk(admin.token, 'tk-3', 'zu kurz')).rejects.toMatchObject({ status: 400 });

    const accepted = await scanApi.acceptRisk(
      admin.token,
      'tk-3',
      'Die restlichen Bausteine sind nicht produktiv im Einsatz, daher wird das Risiko knowingly genommen.',
    );
    expect(accepted.status).toBe('RiskAccepted');
    expect(accepted.audit.some((a) => a.action === 'RiskAccepted')).toBe(true);
  });

  it('admin kann Benutzer anlegen und deaktivieren sowie Teams anlegen und das Audit lesen', async () => {
    const admin = await login('admin@sims.example');
    const teams = await scanApi.listTeams(admin.token);
    const created = await scanApi.createUser(admin.token, {
      email: 'neu@sims.example',
      name: 'Neue Person',
      role: 'Developer',
      teamId: teams[0].id,
    });
    expect(created.email).toBe('neu@sims.example');

    await scanApi.disableUser(admin.token, created.id);
    const users = await scanApi.listUsers(admin.token);
    expect(users.find((u) => u.id === created.id)?.active).toBe(false);

    const team = await scanApi.createTeam(admin.token, 'Team Gelb');
    expect(team.name).toBe('Team Gelb');

    const audit = await scanApi.listAudit(admin.token);
    expect(audit.length).toBeGreaterThan(0);
    expect(audit.some((a) => a.action === 'TicketEscalated')).toBe(true);
  });

  it('verweigert die Verwaltung für Nicht-Admins', async () => {
    const dev = await login('dev@sims.example', false);
    await expect(scanApi.listUsers(dev.token)).rejects.toMatchObject({ status: 403 });
    await expect(scanApi.listAudit(dev.token)).rejects.toMatchObject({ status: 403 });
  });
});

describe('News (E5)', () => {
  it('liefert öffentliche Meldungen ohne Anmeldung', async () => {
    const items = await newsApi.list();
    expect(items.length).toBeGreaterThan(0);
    expect(items[0].title).toBeTruthy();
    expect(items[0].url).toMatch(/^http/);
  });
});
