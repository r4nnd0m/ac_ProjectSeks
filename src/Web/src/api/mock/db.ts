// Erfundene Testdaten für den Demo-Modus (VITE_USE_MOCKS=true).
//
// ALLE Daten in dieser Datei sind ausgedacht und ersetzen KEINE echten
// Scanurteile, Ticketregeln oder Eskalationen. Diese kommen später vom
// Backend (Worker/Scan-API). Der Demo-Modus ist in der Oberfläche sichtbar
// gekennzeichnet.
//
// Der Zustand liegt im sessionStorage, damit die Demo einen Neuladen des
// Browsers übersteht. In Testumgebungen ohne sessionStorage wird rein im
// Arbeitsspeicher gearbeitet.

import type {
  AuditEntryDto,
  FindingDto,
  NewsItemDto,
  ProjectDto,
  ScanDetailDto,
  SessionUserDto,
  TeamDto,
  TicketDetailDto,
  UserDto,
} from '../../types';

export interface MockUserRecord extends UserDto {
  /** Nur in der Demo: Klartext-Passwort der erfundenen Testkonten. */
  password: string;
}

interface PendingScan {
  timerScanning: ReturnType<typeof setTimeout>;
  timerDone: ReturnType<typeof setTimeout>;
}

interface MockDb {
  teams: TeamDto[];
  users: MockUserRecord[];
  projects: ProjectDto[];
  scans: ScanDetailDto[];
  tickets: TicketDetailDto[];
  audit: AuditEntryDto[];
  news: NewsItemDto[];
  /** token -> Sessionnutzer */
  sessions: Record<string, SessionUserDto>;
  /** mfaToken -> userId (Zwischenschritt der Anmeldung) */
  pendingMfa: Record<string, string>;
  /** userId -> Base32-Secret aus der 2FA-Einrichtung */
  mfaSecrets: Record<string, string>;
  seq: number;
}

const STORAGE_KEY = 'sims-demo-db-v1';

/** In der Demo immer gültiger Einmalcode fürs TOTP-Feld. */
export const DEMO_MFA_CODE = '123456';

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

  const users: MockUserRecord[] = [
    {
      id: 'u-admin', email: 'admin@sims.example', name: 'Alex Admin', role: 'Admin',
      teamId: teamBlau.id, teamName: teamBlau.name, active: true, mfaEnabled: true, password: 'demo1234',
    },
    {
      id: 'u-lead', email: 'lead@sims.example', name: 'Anna Leitner', role: 'Lead',
      teamId: teamBlau.id, teamName: teamBlau.name, active: true, mfaEnabled: true, password: 'demo1234',
    },
    {
      id: 'u-dev', email: 'dev@sims.example', name: 'Ben Entwickler', role: 'Developer',
      teamId: teamBlau.id, teamName: teamBlau.name, active: true, mfaEnabled: false, password: 'demo1234',
    },
    {
      id: 'u-dev2', email: 'dev2@sims.example', name: 'Clara Code', role: 'Developer',
      teamId: teamGruen.id, teamName: teamGruen.name, active: true, mfaEnabled: false, password: 'demo1234',
    },
  ];

  const projects: ProjectDto[] = [
    { id: 'p-shelf', name: 'Shelf-Service', teamId: teamBlau.id, teamName: teamBlau.name, createdAt: daysAgo(30) },
    { id: 'p-gateway', name: 'City-Gateway', teamId: teamGruen.id, teamName: teamGruen.name, createdAt: daysAgo(12) },
  ];

  const findingsNogo: FindingDto[] = [
    {
      id: 'f-nogo-1', componentId: 'c-1', componentName: 'log4j-core', componentVersion: '2.14.1',
      vulnId: 'osv:GHSA-jfh8-c2jp-5v3q', cve: 'CVE-2021-44228', severity: 'Critical', isKev: true, epss: 0.97,
    },
    {
      id: 'f-nogo-2', componentId: 'c-2', componentName: 'commons-text', componentVersion: '1.9',
      vulnId: 'osv:GHSA-72hv-8253-57qq', cve: 'CVE-2022-42889', severity: 'High', isKev: false, epss: 0.30,
    },
  ];

  const findingsGwc: FindingDto[] = [
    {
      id: 'f-gwc-1', componentId: 'c-3', componentName: 'Newtonsoft.Json', componentVersion: '12.0.1',
      vulnId: 'osv:GHSA-4qm4-892v-fufa', cve: null, severity: 'High', isKev: false, epss: 0.2,
    },
    {
      id: 'f-gwc-2', componentId: 'c-4', componentName: 'System.Text.RegularExpressions', componentVersion: '4.3.0',
      vulnId: 'osv:GHSA-jhpv-4q4f-43ww', cve: 'CVE-2019-0584', severity: 'Low', isKev: false, epss: 0.05,
    },
  ];

  const scans: ScanDetailDto[] = [
    {
      id: 's-go', projectId: 'p-shelf', version: '1.2.0', status: 'Scanned', verdict: 'Go',
      createdAt: daysAgo(9), finishedAt: daysAgo(9), errorMessage: null, findings: [],
    },
    {
      id: 's-nogo', projectId: 'p-shelf', version: '1.3.0', status: 'Scanned', verdict: 'NoGo',
      createdAt: daysAgo(5), finishedAt: daysAgo(5), errorMessage: null, findings: findingsNogo,
    },
    {
      id: 's-gwc', projectId: 'p-shelf', version: '1.3.1', status: 'Scanned', verdict: 'GoWithConditions',
      createdAt: daysAgo(2), finishedAt: daysAgo(2), errorMessage: null, findings: findingsGwc,
    },
    {
      id: 's-failed', projectId: 'p-gateway', version: '0.9.0', status: 'Failed', verdict: null,
      createdAt: daysAgo(1), finishedAt: daysAgo(1),
      errorMessage: '[DEMO] Schwachstellenquelle nicht erreichbar – der Worker behandelt das als Scanfehler (B4/B5).',
      findings: [],
    },
  ];

  const tickets: TicketDetailDto[] = [
    {
      id: 'tk-1', title: 'Log4Shell in log4j-core 2.14.1', severity: 'Critical', status: 'Escalated',
      escalationLevel: 3, assignedRole: 'Admin', dueAt: null, assigneeName: null,
      teamId: teamBlau.id, projectId: 'p-shelf', createdAt: daysAgo(5), updatedAt: daysAgo(4),
      description: 'Automatisch aus Scan s-nogo erzeugt (C3). KEV-Eintrag vorhanden – Start auf Stufe 3 mit Admin (C4).',
      scanId: 's-nogo', findingId: 'f-nogo-1',
      audit: [],
    },
    {
      id: 'tk-2', title: 'Newtonsoft.Json 12.0.1 mit bekannter Schwachstelle', severity: 'High', status: 'Escalated',
      escalationLevel: 2, assignedRole: 'Lead', dueAt: hoursFromNow(24), assigneeName: null,
      teamId: teamBlau.id, projectId: 'p-shelf', createdAt: daysAgo(2), updatedAt: hoursFromNow(-6),
      description: 'Automatisch aus Scan s-gwc erzeugt (C3). Der Rolle Lead zugeordnet – persönlich hat es noch niemand übernommen (C2).',
      scanId: 's-gwc', findingId: 'f-gwc-1',
      audit: [
        {
          id: 'au-seed-1', userId: '00000000-0000-0000-0000-000000000000', userName: 'SIMS Worker',
          action: 'TicketEscalated', ticketId: 'tk-2', details: 'Stufe 1 auf 2', timestamp: hoursFromNow(-6),
        },
      ],
    },
    {
      id: 'tk-3', title: 'System.Text.RegularExpressions veraltet', severity: 'Low', status: 'Open',
      escalationLevel: 1, assignedRole: null, dueAt: null, assigneeName: null,
      teamId: teamBlau.id, projectId: 'p-shelf', createdAt: daysAgo(2), updatedAt: daysAgo(2),
      description: 'Low-Fund ohne reguläre Frist (C4).', scanId: 's-gwc', findingId: 'f-gwc-2',
      audit: [],
    },
  ];

  const audit: AuditEntryDto[] = [
    ...tickets.flatMap((t) => t.audit),
    {
      id: 'au-seed-2', userId: '00000000-0000-0000-0000-000000000000', userName: 'SIMS Worker',
      action: 'TicketEscalated', ticketId: 'tk-1', details: 'Stufe 2 auf 3', timestamp: daysAgo(4),
    },
  ];

  // Fremde Inhalte werden als reiner Text ausgeliefert und in der Oberfläche
  // ebenfalls nur als Text gerendert (E5).
  const news: NewsItemDto[] = [
    {
      id: 'n-1', title: '[DEMO] BSI warnt vor aktiv ausgenutzter Lücke in Beispieldienst',
      source: 'BSI (Demo)', publishedAt: daysAgo(1), url: 'https://example.org/sims/demo-hinweis-1',
      summary: "<script>alert('fremder Inhalt')</script>Dieser Text stammt aus einer fremden Quelle und wird absichtlich als reiner Text angezeigt.",
    },
    {
      id: 'n-2', title: '[DEMO] CISA-Katalog: neues KEV-Eintrag für weit verbreitete Bibliothek',
      source: 'CISA KEV (Demo)', publishedAt: daysAgo(3), url: 'https://example.org/sims/demo-hinweis-2',
      summary: 'Erfundene Meldung nur für die Vorführung. Link zeigt auf example.org.',
    },
    {
      id: 'n-3', title: '[DEMO] SIMS-Team probt den Freigabeablauf',
      source: 'SIMS intern (Demo)', publishedAt: daysAgo(6), url: 'https://example.org/sims/demo-hinweis-3',
      summary: 'Erinnerung: Echte Urteile erzeugt ausschließlich das Backend.',
    },
  ];

  return {
    teams: [teamBlau, teamGruen],
    users,
    projects,
    scans,
    tickets,
    audit,
    news,
    sessions: {},
    pendingMfa: {},
    mfaSecrets: {},
    seq: 100,
  };
}

function loadDb(): MockDb {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as MockDb;
      // Timer/Session-Felder dürfen nicht aus altem Zustand stammen.
      parsed.sessions = {};
      parsed.pendingMfa = {};
      return parsed;
    }
  } catch {
    /* sessionStorage nicht verfügbar (Tests) oder beschädigt -> neu füllen */
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
    /* ohne sessionStorage bleibt alles nur im Arbeitsspeicher */
  }
}

/** Demo-Zustand zurücksetzen (Button auf der Hinweise-Seite, Tests). */
export function resetDemoData(): void {
  for (const t of pendingTimers.values()) {
    clearTimeout(t.timerScanning);
    clearTimeout(t.timerDone);
  }
  pendingTimers.clear();
  const fresh = seed();
  Object.assign(db, fresh);
  persist();
}

export function recordAudit(
  user: { id: string; name: string },
  action: string,
  ticketId: string | null,
  details: string,
): AuditEntryDto {
  const entry: AuditEntryDto = {
    id: nextId('au'),
    userId: user.id,
    userName: user.name,
    action,
    ticketId,
    details,
    timestamp: new Date().toISOString(),
  };
  db.audit.unshift(entry);
  return entry;
}

/**
 * Läuft einen erfundenen Scan zeitlich durch: Pending -> Scanning -> Ergebnis.
 * Ergebnis: GoWithConditions mit erfundenen Funden plus automatisch erzeugtem
 * Ticket – analog zum vorgesehenen Workerablauf (C3), aber ohne echte Regeln.
 * Enthält die Versionszeichenkette "fail", endet der Scan als Failed (ohne
 * Urteil) – wie in B5 für Quellenfehler beschrieben.
 */
export function scheduleScan(scan: ScanDetailDto): void {
  if (typeof setTimeout !== 'function') {
    // Testumgebung: Ergebnis sofort fertigstellen, damit Polling endet.
    finishScan(scan, scan.version.includes('fail'));
    return;
  }
  const timerScanning = setTimeout(() => {
    scan.status = 'Scanning';
    persist();
  }, 2_500);
  const shouldFail = scan.version.includes('fail');
  const timerDone = setTimeout(() => {
    finishScan(scan, shouldFail);
  }, 7_000);
  pendingTimers.set(scan.id, { timerScanning, timerDone });
}

function finishScan(scan: ScanDetailDto, failed: boolean): void {
  pendingTimers.delete(scan.id);
  if (failed) {
    scan.status = 'Failed';
    scan.verdict = null;
    scan.finishedAt = new Date().toISOString();
    scan.errorMessage = '[DEMO] Die erfundene Schwachstellenquelle hat einen Fehler geworfen. Kein Urteil möglich (wie in B5).';
    persist();
    return;
  }
  scan.status = 'Scanned';
  scan.verdict = 'GoWithConditions';
  scan.finishedAt = new Date().toISOString();
  scan.errorMessage = null;
  scan.findings = [
    {
      id: nextId('f'), componentId: nextId('c'), componentName: 'Beispiel.Kasse.dll',
      componentVersion: '1.2.3', vulnId: 'osv:DEMO-2026-0001', cve: 'CVE-2026-00001',
      severity: 'High', isKev: false, epss: 0.2,
    },
    {
      id: nextId('f'), componentId: nextId('c'), componentName: 'Beispiel.Druck.dll',
      componentVersion: '0.9.9', vulnId: 'osv:DEMO-2026-0002', cve: null,
      severity: 'Medium', isKev: false, epss: 0.03,
    },
  ];
  const project = db.projects.find((p) => p.id === scan.projectId);
  const high = scan.findings[0];
  const ticket: TicketDetailDto = {
    id: nextId('tk'),
    title: `[DEMO] ${high.componentName} ${high.componentVersion} – ${high.cve ?? high.vulnId}`,
    severity: high.severity,
    status: 'Open',
    escalationLevel: 1,
    assignedRole: null,
    dueAt: hoursFromNow(24),
    assigneeName: null,
    teamId: project?.teamId ?? '',
    projectId: scan.projectId,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    description: '[DEMO] Automatisch aus einem Scan erzeugt (C3). Erfundener Hoch-Fund – Startstufe 1, Frist 24 h (C4).',
    scanId: scan.id,
    findingId: high.id,
    audit: [],
  };
  db.tickets.unshift(ticket);
  recordAudit(
    { id: '00000000-0000-0000-0000-000000000000', name: 'SIMS Worker' },
    'TicketCreated', ticket.id, 'Automatisch aus Scan ' + scan.id + ' erzeugt [DEMO]',
  );
  persist();
}
