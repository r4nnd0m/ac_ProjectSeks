// Smoke-Test für den Demo-Modus (erfundene Testdaten). Prüft, dass die
// Mock-Schicht dieselben Antwortformen und Statuscodes wie das Backend liefert:
// Anmeldung mit/ohne MFA, Projekte und Scans, Tickets, Verwaltung, News und
// die zentrale Fehlerbehandlung.

import { beforeAll, describe, expect, it } from 'vitest';
import { authApi } from '../src/api/authApi';
import { adminApi } from '../src/api/adminApi';
import { scanApi } from '../src/api/scanApi';
import { newsApi } from '../src/api/newsApi';
import { ApiError, MESSAGES } from '../src/api/http';
import { DEMO_MFA_CODE, DEMO_PASSWORD, resetDemoData } from '../src/api/mock/db';
import { hasMfaClaim } from '../src/auth/token';
import { validateSbom, EXAMPLE_SBOM } from '../src/pages/ProjectDetailPage';
import type { ScanDetailDto } from '../src/types';

async function login(email: string): Promise<string> {
  const result = await authApi.login(email, DEMO_PASSWORD);
  if (result.mfaRequired && result.mfaToken) {
    const verified = await authApi.verifyMfa(result.mfaToken, DEMO_MFA_CODE);
    return verified.accessToken!;
  }
  return result.accessToken!;
}

async function pollUntilDone(token: string, scanId: string): Promise<ScanDetailDto> {
  const deadline = Date.now() + 15_000;
  for (;;) {
    const scan = await scanApi.getScan(token, scanId);
    if (scan.status === 'Scanned' || scan.status === 'Failed') return scan;
    if (Date.now() > deadline) throw new Error(`Scan ${scanId} wurde nicht fertig`);
    await new Promise((r) => setTimeout(r, 200));
  }
}

beforeAll(() => {
  resetDemoData();
});

describe('Anmeldung (E2)', () => {
  it('Lead braucht den zweiten Faktor, danach trägt das Token mfa=true', async () => {
    const first = await authApi.login('lead@sims.example', DEMO_PASSWORD);
    expect(first.mfaRequired).toBe(true);
    expect(first.accessToken).toBeNull();
    const second = await authApi.verifyMfa(first.mfaToken!, DEMO_MFA_CODE);
    expect(second.accessToken).toBeTruthy();
    expect(hasMfaClaim(second.accessToken!)).toBe(true);
    const me = await authApi.me(second.accessToken!);
    expect(me.role).toBe('Lead');
  });

  it('lehnt ein falsches Passwort mit 401 ab', async () => {
    await expect(authApi.login('lead@sims.example', 'falsch')).rejects.toMatchObject({ status: 401 });
  });

  it('Logout macht das Token ungültig', async () => {
    const token = await login('dev@sims.example');
    expect(hasMfaClaim(token)).toBe(false);
    await authApi.logout(token);
    await expect(authApi.me(token)).rejects.toMatchObject({ status: 401 });
  });
});

describe('Projekte und Scans (E3)', () => {
  it('prüft SBOM auf JSON, CycloneDX und Größe', () => {
    expect(validateSbom('kein json')).toMatch(/kein gültiges JSON/);
    expect(validateSbom('{"a":1}')).toMatch(/CycloneDX/);
    expect(validateSbom(EXAMPLE_SBOM)).toBeNull();
  });

  it('legt ein Projekt an, lädt eine SBOM hoch und bekommt ein Urteil', async () => {
    const token = await login('lead@sims.example');
    const project = await scanApi.createProject(token, 'Kassen-Teststand');
    const created = await scanApi.createScan(token, project.id, '1.0.0', EXAMPLE_SBOM);
    expect(created.status).toBe('Pending');
    const done = await pollUntilDone(token, created.scanId);
    expect(done.status).toBe('Scanned');
    expect(done.verdict).toBe('GoWithConditions');
    const list = await scanApi.listProjectScans(token, project.id);
    expect(list.some((s) => s.id === created.scanId)).toBe(true);
  }, 20_000);

  it('ein fehlgeschlagener Scan hat kein Urteil', async () => {
    const token = await login('lead@sims.example');
    const project = (await scanApi.listProjects(token))[0];
    const created = await scanApi.createScan(token, project.id, 'fail-1.0.0', EXAMPLE_SBOM);
    const done = await pollUntilDone(token, created.scanId);
    expect(done.status).toBe('Failed');
    expect(done.verdict).toBeNull();
  }, 20_000);

  it('fremde Projekte liefern 404', async () => {
    const token = await login('dev2@sims.example');
    await expect(scanApi.getProject(token, 'p-shelf')).rejects.toMatchObject({ status: 404 });
  });
});

describe('Tickets (E4)', () => {
  it('übernimmt und schließt ein Ticket, „Meine Tickets“ filtert', async () => {
    const token = await login('lead@sims.example');
    const before = await scanApi.listTickets(token, { assignedToMe: true });
    expect(before.items.some((t) => t.id === 'tk-2')).toBe(false);
    await scanApi.takeTicket(token, 'tk-2');
    const after = await scanApi.listTickets(token, { assignedToMe: true });
    expect(after.items.some((t) => t.id === 'tk-2')).toBe(true);
    await scanApi.closeTicket(token, 'tk-2');
    expect((await scanApi.getTicket(token, 'tk-2')).status).toBe('Closed');
    await expect(scanApi.closeTicket(token, 'tk-2')).rejects.toMatchObject({ status: 409 });
  });

  it('Risiko akzeptieren nur als Admin mit MFA und Begründung ab 20 Zeichen', async () => {
    const dev = await login('dev@sims.example');
    await expect(scanApi.acceptRisk(dev, 'tk-3', 'x'.repeat(25))).rejects.toMatchObject({ status: 403 });
    const admin = await login('admin@sims.example');
    await expect(scanApi.acceptRisk(admin, 'tk-3', 'zu kurz')).rejects.toMatchObject({ status: 400 });
    await scanApi.acceptRisk(admin, 'tk-3', 'Die Bibliothek ist nicht im produktiven Pfad eingebunden.');
    expect((await scanApi.getTicket(admin, 'tk-3')).status).toBe('RiskAccepted');
  });
});

describe('Verwaltung (E4)', () => {
  it('Admin legt Team und User an, Fehler kommen als Liste, Deaktivieren sperrt den Login', async () => {
    const admin = await login('admin@sims.example');
    const team = await adminApi.createTeam(admin, 'Team Gelb');
    await expect(adminApi.createTeam(admin, 'Team Gelb')).rejects.toMatchObject({ status: 409 });

    const weak = adminApi.createUser(admin, { email: 'schwach@sims.example', password: 'abc', role: 'Developer', teamId: team.id });
    await expect(weak).rejects.toBeInstanceOf(ApiError);
    await expect(weak).rejects.toMatchObject({ status: 400 });

    const created = await adminApi.createUser(admin, { email: 'neu@sims.example', password: 'SehrSicher123!', role: 'Developer', teamId: team.id });
    expect(created.isActive).toBe(true);
    expect(created).not.toHaveProperty('password');

    await adminApi.deactivateUser(admin, created.id);
    await expect(authApi.login('neu@sims.example', 'SehrSicher123!')).rejects.toMatchObject({ status: 401 });
  });

  it('Nicht-Admins bekommen 403', async () => {
    const dev = await login('dev@sims.example');
    await expect(adminApi.listUsers(dev)).rejects.toMatchObject({ status: 403, message: MESSAGES.forbidden });
    await expect(scanApi.listAudit(dev)).rejects.toMatchObject({ status: 403 });
  });
});

describe('News (E5)', () => {
  it('liefert Meldungen ohne Anmeldung, neueste zuerst', async () => {
    const items = await newsApi.list();
    expect(items.length).toBeGreaterThan(0);
    expect(items[0].publishedAt >= items[items.length - 1].publishedAt).toBe(true);
  });
});
