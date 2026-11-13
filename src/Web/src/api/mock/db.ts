// Erfundene Testdaten für den Demo-Modus (VITE_USE_MOCKS=true).
//
// ALLE Daten hier sind ausgedacht. Echte Urteile, Ticketregeln und
// Eskalationen kommen ausschließlich vom Backend. Die Datensätze haben
// dieselbe Form wie die API-Antworten (src/types.ts) plus einige interne
// Felder (Team, Passwort), die der Pseudo-Server nie herausgibt.
//
// Der Zustand liegt im sessionStorage, damit die Demo ein Neuladen übersteht.
// Er enthält KEINE Tokens. In Tests ohne sessionStorage bleibt alles im Speicher.

import type { AuditEntryDto, NewsItemDto, ProjectDto, Role, ScanDetailDto, TeamDto, TicketDto } from '../../types';

export interface MockUser {
  id: string;
  email: string;
  role: Role;
  teamId: string;
  isActive: boolean;
  mfaEnabled: boolean;
  /** Nur in der Demo: Klartext-Passwort der erfundenen Testkonten. */
  password: string;
}

export interface MockProject extends ProjectDto {
  teamId: string;
}

export interface MockTicket extends TicketDto {
  teamId: string;
  assigneeId: string | null;
}

interface PendingScan {
  timerScanning: ReturnType<typeof setTimeout>;
  timerDone: ReturnType<typeof setTimeout>;
}

export interface MockSession {
  userId: string;
  mfa: boolean;
}

interface MockDb {
  teams: TeamDto[];
  users: MockUser[];
  projects: MockProject[];
  scans: ScanDetailDto[];
  tickets: MockTicket[];
  audit: AuditEntryDto[];
  news: NewsItemDto[];
  /** token -> Sitzung (nie im sessionStorage) */
  sessions: Record<string, MockSession>;
  /** mfaToken -> userId */
  pendingMfa: Record<string, string>;
}

const STORAGE_KEY = 'sims-demo-db-v2';

/** In der Demo immer gültiger Einmalcode. */
export const DEMO_MFA_CODE = '123456';
/** Passwort aller Demo-Konten. */
export const DEMO_PASSWORD = 'demo1234';

function hoursFromNow(h: number): string {
  return new Date(Date.now() + h * 3_600_000).toISOString();
}
function daysAgo(d: number): string {
  return new Date(Date.now() - d * 86_400_000).toISOString();
}

let seqCounter = 100;
export function nextId(prefix: string): string {
  seqCounter += 1;
  return `${prefix}-${seqCounter}`;
}

function seed(): MockDb {
  const teamBlau: TeamDto = { id: 'team-blue', name: 'Team Blau' };
  const teamGruen: TeamDto = { id: 'team-green', name: 'Team Grün' };

  const users: MockUser[] = [
    { id: 'u-admin', email: 'admin@sims.example', role: 'Admin', teamId: teamBlau.id, isActive: true, mfaEnabled: true, password: DEMO_PASSWORD },
    { id: 'u-lead', email: 'lead@sims.example', role: 'Lead', teamId: teamBlau.id, isActive: true, mfaEnabled: true, password: DEMO_PASSWORD },
    { id: 'u-dev', email: 'dev@sims.example', role: 'Developer', teamId: teamBlau.id, isActive: true, mfaEnabled: false, password: DEMO_PASSWORD },
    { id: 'u-dev2', email: 'dev2@sims.example', role: 'Developer', teamId: teamGruen.id, isActive: true, mfaEnabled: false, password: DEMO_PASSWORD },
  ];

  const projects: MockProject[] = [
    { id: 'p-shelf', name: 'Shelf-Service', teamId: teamBlau.id, createdAt: daysAgo(30) },
    { id: 'p-gateway', name: 'City-Gateway', teamId: teamGruen.id, createdAt: daysAgo(12) },
  ];

  const scans: ScanDetailDto[] = [
    { id: 's-go', projectId: 'p-shelf', version: '1.2.0', status: 'Scanned', verdict: 'Go', createdAt: daysAgo(9), finishedAt: daysAgo(9), errorMessage: null, findings: [] },
    {
      id: 's-nogo', projectId: 'p-shelf', version: '1.3.0', status: 'Scanned', verdict: 'NoGo', createdAt: daysAgo(5), finishedAt: daysAgo(5), errorMessage: null,
      findings: [
        { id: 'f-nogo-1', component: 'log4j-core', componentVersion: '2.14.1', vulnId: 'GHSA-jfh8-c2jp-5v3q', cve: 'CVE-2021-44228', severity: 'Critical', isKev: true, epss: 0.97 },
        { id: 'f-nogo-2', component: 'commons-text', componentVersion: '1.9', vulnId: 'GHSA-599f-7c49-w659', cve: 'CVE-2022-42889', severity: 'High', isKev: false, epss: 0.3 },
      ],
    },
    {
      id: 's-gwc', projectId: 'p-shelf', version: '1.3.1', status: 'Scanned', verdict: 'GoWithConditions', createdAt: daysAgo(2), finishedAt: daysAgo(2), errorMessage: null,
      findings: [
        { id: 'f-gwc-1', component: 'Newtonsoft.Json', componentVersion: '12.0.1', vulnId: 'GHSA-5crp-9r3c-p9vr', cve: null, severity: 'High', isKev: false, epss: 0.2 },
        { id: 'f-gwc-2', component: 'System.Text.RegularExpressions', componentVersion: '4.3.0', vulnId: 'GHSA-cmhx-cq75-c4mj', cve: 'CVE-2019-0820', severity: 'Low', isKev: false, epss: 0.05 },
      ],
    },
    {
      id: 's-failed', projectId: 'p-gateway', version: '0.9.0', status: 'Failed', verdict: null, createdAt: daysAgo(1), finishedAt: daysAgo(1),
      errorMessage: '[DEMO] Schwachstellenquelle nicht erreichbar.', findings: [],
    },
  ];

  const ticketBase = { cve: null, system: null, reporterName: 'SIMS Worker', closedAt: null, assigneeId: null };
  const tickets: MockTicket[] = [
    {
      ...ticketBase, id: 'tk-1', title: 'Log4Shell in log4j-core 2.14.1', severity: 'Critical', status: 'Escalated', escalationLevel: 3,
      assignedRole: 'Admin', dueAt: null, assigneeName: null, teamId: teamBlau.id, projectId: 'p-shelf', createdAt: daysAgo(5),
      description: '[DEMO] Automatisch aus Scan s-nogo erzeugt. KEV-Eintrag vorhanden.', cve: 'CVE-2021-44228',
    },
    {
      ...ticketBase, id: 'tk-2', title: 'Newtonsoft.Json 12.0.1 mit bekannter Schwachstelle', severity: 'High', status: 'Escalated', escalationLevel: 2,
      assignedRole: 'Lead', dueAt: hoursFromNow(24), assigneeName: null, teamId: teamBlau.id, projectId: 'p-shelf', createdAt: daysAgo(2),
      description: '[DEMO] Der Rolle Lead zugeordnet – persönlich hat es noch niemand übernommen.',
    },
    {
      ...ticketBase, id: 'tk-3', title: 'System.Text.RegularExpressions veraltet', severity: 'Low', status: 'Open', escalationLevel: 1,
      assignedRole: null, dueAt: null, assigneeName: null, teamId: teamBlau.id, projectId: 'p-shelf', createdAt: daysAgo(2),
      description: '[DEMO] Low-Fund ohne reguläre Frist.', cve: 'CVE-2019-0820',
    },
  ];

  const audit: AuditEntryDto[] = [
    { id: 'au-seed-1', userName: 'SIMS Worker', action: 'TicketEscalated', ticketId: 'tk-2', details: 'Stufe 1 auf 2', timestamp: hoursFromNow(-6) },
    { id: 'au-seed-2', userName: 'SIMS Worker', action: 'TicketEscalated', ticketId: 'tk-1', details: 'Stufe 2 auf 3', timestamp: daysAgo(4) },
  ];

  // Fremder Inhalt: wird in der Oberfläche nur als Text ausgegeben (E5).
  const news: NewsItemDto[] = [
    { title: "[DEMO] <script>alert('x')</script> Warnung vor aktiv ausgenutzter Lücke", source: 'BSI (Demo)', publishedAt: daysAgo(1), link: 'https://example.org/sims/demo-1' },
    { title: '[DEMO] Neuer KEV-Eintrag für weit verbreitete Bibliothek', source: 'CISA KEV (Demo)', publishedAt: daysAgo(3), link: 'https://example.org/sims/demo-2' },
    { title: '[DEMO] Unsicherer Link wird nicht angezeigt', source: 'Demo', publishedAt: daysAgo(6), link: 'javascript:alert(1)' },
  ];

  return { teams: [teamBlau, teamGruen], users, projects, scans, tickets, audit, news, sessions: {}, pendingMfa: {} };
}

function loadDb(): MockDb {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as MockDb;
      parsed.sessions = {};
      parsed.pendingMfa = {};
      return parsed;
    }
  } catch {
    /* kein sessionStorage (Tests) oder beschädigt -> neu füllen */
  }
  return seed();
}

export const db: MockDb = loadDb();

const pendingTimers = new Map<string, PendingScan>();

export function persist(): void {
  try {
    const copy: MockDb = { ...db, sessions: {}, pendingMfa: {} };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(copy));
  } catch {
    /* ohne sessionStorage nur im Arbeitsspeicher */
  }
}

export function resetDemoData(): void {
  for (const t of pendingTimers.values()) {
    clearTimeout(t.timerScanning);
    clearTimeout(t.timerDone);
  }
  pendingTimers.clear();
  Object.assign(db, seed());
  persist();
}

export function recordAudit(userName: string, action: string, ticketId: string | null, details: string): void {
  db.audit.unshift({ id: nextId('au'), userName, action, ticketId, details, timestamp: new Date().toISOString() });
}

/**
 * Erfundener Scanablauf: Pending -> Scanning -> Ergebnis. Enthält die Version
 * „fail“, endet der Scan als Failed ohne Urteil.
 */
export function scheduleScan(scan: ScanDetailDto): void {
  const shouldFail = scan.version.includes('fail');
  const timerScanning = setTimeout(() => {
    scan.status = 'Scanning';
    persist();
  }, 1_500);
  const timerDone = setTimeout(() => finishScan(scan, shouldFail), 4_000);
  pendingTimers.set(scan.id, { timerScanning, timerDone });
}

function finishScan(scan: ScanDetailDto, failed: boolean): void {
  pendingTimers.delete(scan.id);
  scan.finishedAt = new Date().toISOString();
  if (failed) {
    scan.status = 'Failed';
    scan.verdict = null;
    scan.errorMessage = '[DEMO] Die erfundene Schwachstellenquelle hat einen Fehler geworfen. Kein Urteil möglich.';
    persist();
    return;
  }
  scan.status = 'Scanned';
  scan.verdict = 'GoWithConditions';
  scan.errorMessage = null;
  scan.findings = [
    { id: nextId('f'), component: 'Beispiel.Kasse', componentVersion: '1.2.3', vulnId: 'DEMO-2026-0001', cve: 'CVE-2026-00001', severity: 'High', isKev: false, epss: 0.2 },
    { id: nextId('f'), component: 'Beispiel.Druck', componentVersion: '0.9.9', vulnId: 'DEMO-2026-0002', cve: null, severity: 'Medium', isKev: false, epss: 0.03 },
  ];
  const project = db.projects.find((p) => p.id === scan.projectId);
  const high = scan.findings[0];
  const ticket: MockTicket = {
    id: nextId('tk'),
    title: `[DEMO] ${high.component} ${high.componentVersion} – ${high.cve ?? high.vulnId}`,
    description: '[DEMO] Automatisch aus einem Scan erzeugt.',
    severity: high.severity,
    status: 'Open',
    cve: high.cve,
    system: null,
    projectId: scan.projectId,
    reporterName: 'SIMS Worker',
    assigneeName: null,
    assigneeId: null,
    assignedRole: null,
    escalationLevel: 1,
    dueAt: hoursFromNow(24),
    createdAt: new Date().toISOString(),
    closedAt: null,
    teamId: project?.teamId ?? '',
  };
  db.tickets.unshift(ticket);
  recordAudit('SIMS Worker', 'TicketCreated', ticket.id, `Automatisch aus Scan ${scan.id} erzeugt [DEMO]`);
  persist();
}
