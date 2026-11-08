// Der „Pseudo-Server“ des Demo-Modus: bildet die API-Aufrufe auf die
// erfundene Speichersimulation ab. Rechte- und Teamprüfungen sind hier nur
// nachgebaut, damit die Demo realistisch wirkt – verbindlich ist später die
// Prüfung im Backend.

import type {
  AuditEntryDto,
  AuthSessionDto,
  LoginResultDto,
  MfaSetupDto,
  NewsItemDto,
  PagedDto,
  ProjectDto,
  ScanDetailDto,
  ScanListItemDto,
  SessionUserDto,
  TeamDto,
  TicketDetailDto,
  TicketListItemDto,
  TicketQuery,
  UserDto,
} from '../../types';
import { ApiError } from '../http';
import {
  DEMO_MFA_CODE,
  db,
  nextId,
  persist,
  recordAudit,
  scheduleScan,
  type MockUserRecord,
} from './db';

const LATENCY_MS = 250;

function delay(): Promise<void> {
  return new Promise((r) => setTimeout(r, LATENCY_MS));
}

function toSession(user: MockUserRecord, mfaVerified: boolean): SessionUserDto {
  return {
    id: user.id,
    email: user.email,
    name: user.name,
    role: user.role,
    teamId: user.teamId,
    mfaVerified,
    mfaEnabled: user.mfaEnabled,
  };
}

function session(token: string | null): SessionUserDto {
  if (!token) throw new ApiError(401, 'Bitte melde dich an, um fortzufahren.', 'expired');
  const user = db.sessions[token];
  if (!user) throw new ApiError(401, 'Deine Anmeldung ist abgelaufen. Bitte melde dich erneut an.', 'expired');
  return user;
}

function requireRole(user: SessionUserDto, roles: string[]): void {
  if (!roles.includes(user.role)) {
    throw new ApiError(403, 'Dafür fehlen dir die Rechte. Maßgeblich ist die Prüfung im Backend.', 'forbidden');
  }
}

/** Developer und Lead sehen nur das eigene Team, Admin teamübergreifend (E2). */
function canSeeTeam(user: SessionUserDto, teamId: string): boolean {
  return user.role === 'Admin' || user.teamId === teamId;
}

function projectTeamId(projectId: string): string {
  const project = db.projects.find((p) => p.id === projectId);
  if (!project) throw new ApiError(404, 'Projekt nicht gefunden.', 'notfound');
  return project.teamId;
}

export function ticketListItem(t: TicketDetailDto): TicketListItemDto {
  const { audit: _audit, description: _desc, scanId: _scan, findingId: _finding, updatedAt: _upd, ...list } = t;
  return list;
}

// ---------------------------------------------------------------- Auth ----

export async function mockLogin(email: string, password: string): Promise<LoginResultDto> {
  await delay();
  const user = db.users.find((u) => u.email.toLowerCase() === email.toLowerCase() && u.active);
  if (!user || user.password !== password) {
    throw new ApiError(401, 'E-Mail oder Passwort passt nicht. (Demo: Passwort demo1234, siehe Hinweise)', 'expired');
  }
  if (user.mfaEnabled) {
    const mfaToken = nextId('mfa');
    db.pendingMfa[mfaToken] = user.id;
    return { mfaRequired: true, mfaToken };
  }
  const token = nextId('tok');
  db.sessions[token] = toSession(user, false);
  persist();
  return { mfaRequired: false, session: { token, user: toSession(user, false) } };
}

export async function mockVerifyMfa(mfaToken: string, code: string): Promise<AuthSessionDto> {
  await delay();
  const userId = db.pendingMfa[mfaToken];
  const user = userId ? db.users.find((u) => u.id === userId) : undefined;
  if (!user) throw new ApiError(401, 'Der Anmeldeschritt ist abgelaufen. Bitte neu anmelden.', 'expired');
  if (code.trim() !== DEMO_MFA_CODE) {
    throw new ApiError(401, 'Der Einmalcode ist ungültig. (Demo-Code: 123456)', 'expired');
  }
  delete db.pendingMfa[mfaToken];
  const token = nextId('tok');
  db.sessions[token] = toSession(user, true);
  persist();
  return { token, user: toSession(user, true) };
}

export async function mockMe(token: string): Promise<SessionUserDto> {
  await delay();
  return session(token);
}

export async function mockSetupMfa(token: string): Promise<MfaSetupDto> {
  await delay();
  const user = session(token);
  const secret = 'JBSWY3DPEHPK3PX' + user.id.slice(-3).toUpperCase().replace(/[^A-Z0-9]/g, 'X');
  db.mfaSecrets[user.id] = secret;
  const label = encodeURIComponent(`SIMS Demo: ${user.name}`);
  const url = `otpauth://totp/${label}?secret=${secret}&issuer=SIMS-Demo&algorithm=SHA1&digits=6&period=30`;
  return { secret, otpauthUrl: url };
}

export async function mockConfirmMfa(token: string, code: string): Promise<void> {
  await delay();
  const user = session(token);
  if (code.trim() !== DEMO_MFA_CODE) {
    throw new ApiError(400, 'Der Bestätigungscode ist ungültig. (Demo-Code: 123456)', 'input');
  }
  const record = db.users.find((u) => u.id === user.id);
  if (!record) throw new ApiError(404, 'Benutzer nicht gefunden.', 'notfound');
  record.mfaEnabled = true;
  db.sessions[token] = { ...user, mfaEnabled: true };
  persist();
}

// ------------------------------------------------------------ Projekte ----

export async function mockListProjects(token: string): Promise<ProjectDto[]> {
  await delay();
  const user = session(token);
  return db.projects.filter((p) => canSeeTeam(user, p.teamId));
}

export async function mockCreateProject(token: string, name: string): Promise<ProjectDto> {
  await delay();
  const user = session(token);
  const project: ProjectDto = {
    id: nextId('p'),
    name,
    teamId: user.teamId,
    teamName: db.teams.find((t) => t.id === user.teamId)?.name ?? '',
    createdAt: new Date().toISOString(),
  };
  db.projects.push(project);
  persist();
  return project;
}

export async function mockGetProject(token: string, projectId: string): Promise<ProjectDto> {
  await delay();
  const user = session(token);
  const project = db.projects.find((p) => p.id === projectId);
  if (!project) throw new ApiError(404, 'Projekt nicht gefunden.', 'notfound');
  if (!canSeeTeam(user, project.teamId)) {
    throw new ApiError(403, 'Dieses Projekt gehört einem anderen Team. Für dich nicht sichtbar (B3).', 'forbidden');
  }
  return project;
}

export async function mockListProjectScans(token: string, projectId: string): Promise<ScanListItemDto[]> {
  await mockGetProject(token, projectId);
  return db.scans
    .filter((s) => s.projectId === projectId)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .map(({ findings: _f, errorMessage: _e, ...rest }) => rest);
}

/** B2: POST /projects/{id}/scans?version=… – liefert die neue scanId. */
export async function mockCreateScan(
  token: string,
  projectId: string,
  version: string,
  bomJson: string,
): Promise<ScanListItemDto> {
  await delay();
  await mockGetProject(token, projectId);
  let parsed: unknown;
  try {
    parsed = JSON.parse(bomJson);
  } catch {
    throw new ApiError(400, 'Das war kein gültiges JSON.', 'input');
  }
  const bom = parsed as { bomFormat?: unknown; components?: unknown };
  if (bom.bomFormat !== 'CycloneDX' || !Array.isArray(bom.components)) {
    throw new ApiError(400, 'Erwartet wird CycloneDX-JSON mit einem components-Feld.', 'input');
  }
  const scan: ScanDetailDto = {
    id: nextId('s'),
    projectId,
    version,
    status: 'Pending',
    verdict: null,
    createdAt: new Date().toISOString(),
    finishedAt: null,
    errorMessage: null,
    findings: [],
  };
  db.scans.unshift(scan);
  persist();
  // Momentaufnahme vor der Zeitsteuerung: im Browser bleibt der Scan erst
  // 'Pending'; in Testlaufzeiten ohne Timer endet er sofort.
  const snapshot = { ...scan };
  scheduleScan(scan);
  return snapshot;
}

// --------------------------------------------------------------- Scans ----

export async function mockGetScan(token: string, scanId: string): Promise<ScanDetailDto> {
  await delay();
  const scan = db.scans.find((s) => s.id === scanId);
  if (!scan) throw new ApiError(404, 'Scan nicht gefunden.', 'notfound');
  await mockGetProject(token, scan.projectId);
  return { ...scan, findings: [...scan.findings] };
}

// ------------------------------------------------------------- Tickets ----

export async function mockListTickets(
  token: string,
  query: TicketQuery,
): Promise<PagedDto<TicketListItemDto>> {
  await delay();
  const user = session(token);
  let items = db.tickets.filter((t) => canSeeTeam(user, t.teamId));
  if (query.status) items = items.filter((t) => t.status === query.status);
  if (query.severity) items = items.filter((t) => t.severity === query.severity);
  if (query.mine) items = items.filter((t) => t.assigneeName === user.name);
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 10;
  const start = (page - 1) * pageSize;
  return {
    items: items.slice(start, start + pageSize).map(ticketListItem),
    page,
    pageSize,
    totalCount: items.length,
  };
}

export async function mockGetTicket(token: string, ticketId: string): Promise<TicketDetailDto> {
  await delay();
  const user = session(token);
  const ticket = db.tickets.find((t) => t.id === ticketId);
  if (!ticket) throw new ApiError(404, 'Ticket nicht gefunden.', 'notfound');
  if (!canSeeTeam(user, ticket.teamId)) {
    throw new ApiError(403, 'Dieses Ticket gehört einem anderen Team.', 'forbidden');
  }
  return { ...ticket, audit: [...ticket.audit] };
}

export async function mockCreateTicket(
  token: string,
  input: { title: string; description: string; projectId: string; severity: TicketDetailDto['severity'] },
): Promise<TicketDetailDto> {
  await delay();
  const user = session(token);
  const teamId = projectTeamId(input.projectId);
  if (!canSeeTeam(user, teamId)) {
    throw new ApiError(403, 'Du kannst keine Tickets für ein fremdes Team anlegen.', 'forbidden');
  }
  const ticket: TicketDetailDto = {
    id: nextId('tk'),
    title: input.title,
    description: input.description || null,
    severity: input.severity,
    status: 'Open',
    escalationLevel: 1,
    assignedRole: null,
    dueAt: input.severity === 'Critical' ? new Date(Date.now() + 4 * 3_600_000).toISOString()
      : input.severity === 'High' ? new Date(Date.now() + 24 * 3_600_000).toISOString()
      : input.severity === 'Medium' ? new Date(Date.now() + 72 * 3_600_000).toISOString()
      : null,
    assigneeName: null,
    teamId,
    projectId: input.projectId,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    scanId: null,
    findingId: null,
    audit: [],
  };
  db.tickets.unshift(ticket);
  recordAudit(user, 'TicketCreated', ticket.id, 'Manuell angelegt');
  persist();
  return ticket;
}

export async function mockUpdateTicket(
  token: string,
  ticketId: string,
  patch: { title?: string; description?: string },
): Promise<TicketDetailDto> {
  await delay();
  const user = session(token);
  const ticket = await mockGetTicket(token, ticketId);
  const target = db.tickets.find((t) => t.id === ticket.id)!;
  if (patch.title !== undefined) target.title = patch.title;
  if (patch.description !== undefined) target.description = patch.description || null;
  target.updatedAt = new Date().toISOString();
  target.audit.push(recordAudit(user, 'TicketUpdated', target.id, 'Bearbeitet über die Oberfläche [DEMO]'));
  persist();
  return { ...target };
}

/** C2: POST /tickets/{id}/take – persönliche Übernahme. */
export async function mockTakeTicket(token: string, ticketId: string): Promise<TicketDetailDto> {
  await delay();
  const user = session(token);
  const ticket = await mockGetTicket(token, ticketId);
  const target = db.tickets.find((t) => t.id === ticket.id)!;
  if (target.status === 'Closed' || target.status === 'RiskAccepted') {
    throw new ApiError(409, 'Ein abgeschlossenes Ticket kann nicht übernommen werden.', 'input');
  }
  target.assigneeName = user.name;
  target.status = 'InProgress';
  target.updatedAt = new Date().toISOString();
  target.audit.push(recordAudit(user, 'TicketTaken', target.id, `Persönlich übernommen von ${user.name} [DEMO]`));
  persist();
  return { ...target };
}

export async function mockCloseTicket(token: string, ticketId: string): Promise<TicketDetailDto> {
  await delay();
  const user = session(token);
  const ticket = await mockGetTicket(token, ticketId);
  const target = db.tickets.find((t) => t.id === ticket.id)!;
  target.status = 'Closed';
  target.dueAt = null;
  target.updatedAt = new Date().toISOString();
  target.audit.push(recordAudit(user, 'TicketClosed', target.id, 'Geschlossen [DEMO]'));
  persist();
  return { ...target };
}

/** E4: Nur Admin mit bestätigtem zweitem Faktor, Begründung mindestens 20 Zeichen. */
export async function mockAcceptRisk(token: string, ticketId: string, reason: string): Promise<TicketDetailDto> {
  await delay();
  const user = session(token);
  const ticket = await mockGetTicket(token, ticketId);
  requireRole(user, ['Admin']);
  if (!user.mfaVerified) {
    throw new ApiError(403, 'Risiko nur akzeptierbar, wenn du mit zweitem Faktor angemeldet bist.', 'forbidden');
  }
  if (reason.trim().length < 20) {
    throw new ApiError(400, 'Die Begründung muss mindestens 20 Zeichen haben.', 'input');
  }
  const target = db.tickets.find((t) => t.id === ticket.id)!;
  target.status = 'RiskAccepted';
  target.dueAt = null;
  target.updatedAt = new Date().toISOString();
  target.audit.push(recordAudit(user, 'RiskAccepted', target.id, `Begründung: ${reason.trim()} [DEMO]`));
  persist();
  return { ...target };
}

// --------------------------------------------------------- Verwaltung ----

export async function mockListUsers(token: string): Promise<UserDto[]> {
  await delay();
  const user = session(token);
  requireRole(user, ['Admin']);
  return db.users.map(({ password: _password, ...rest }) => rest);
}

export async function mockCreateUser(
  token: string,
  input: { email: string; name: string; role: UserDto['role']; teamId: string },
): Promise<UserDto> {
  await delay();
  const user = session(token);
  requireRole(user, ['Admin']);
  const team = db.teams.find((t) => t.id === input.teamId);
  if (!team) throw new ApiError(400, 'Team nicht gefunden.', 'input');
  if (db.users.some((u) => u.email.toLowerCase() === input.email.toLowerCase())) {
    throw new ApiError(409, 'Diese E-Mail wird schon verwendet.', 'input');
  }
  const created: MockUserRecord = {
    id: nextId('u'),
    email: input.email,
    name: input.name,
    role: input.role,
    teamId: team.id,
    teamName: team.name,
    active: true,
    mfaEnabled: false,
    password: 'demo1234',
  };
  db.users.push(created);
  recordAudit(user, 'UserCreated', null, `${input.email} (${input.role}) angelegt [DEMO]`);
  persist();
  const { password: _password, ...rest } = created;
  return rest;
}

export async function mockDisableUser(token: string, userId: string): Promise<void> {
  await delay();
  const user = session(token);
  requireRole(user, ['Admin']);
  const target = db.users.find((u) => u.id === userId);
  if (!target) throw new ApiError(404, 'Benutzer nicht gefunden.', 'notfound');
  target.active = false;
  recordAudit(user, 'UserDisabled', null, `${target.email} deaktiviert [DEMO]`);
  persist();
}

export async function mockListTeams(token: string): Promise<TeamDto[]> {
  await delay();
  const user = session(token);
  requireRole(user, ['Admin']);
  return [...db.teams];
}

export async function mockCreateTeam(token: string, name: string): Promise<TeamDto> {
  await delay();
  const user = session(token);
  requireRole(user, ['Admin']);
  const team: TeamDto = { id: nextId('team'), name };
  db.teams.push(team);
  recordAudit(user, 'TeamCreated', null, `${name} angelegt [DEMO]`);
  persist();
  return team;
}

export async function mockListAudit(token: string): Promise<AuditEntryDto[]> {
  await delay();
  const user = session(token);
  requireRole(user, ['Admin']);
  return [...db.audit];
}

// ---------------------------------------------------------------- News ----

/** Öffentlich, ohne Anmeldung (E5). */
export async function mockListNews(): Promise<NewsItemDto[]> {
  await delay();
  return [...db.news].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt));
}
