// „Pseudo-Server“ des Demo-Modus: bildet die API-Aufrufe auf die erfundenen
// Daten ab und liefert dieselben Antwortformen und Statuscodes wie das echte
// Backend. Die Rechteprüfungen sind nur nachgebaut – verbindlich prüft das Backend.

import type {
  AuditEntryDto,
  CreateTicketInput,
  CreateUserInput,
  LoginResponseDto,
  MeDto,
  MfaSetupDto,
  NewsItemDto,
  PagedDto,
  ProjectDto,
  ScanCreatedDto,
  ScanDetailDto,
  ScanSummaryDto,
  TeamDto,
  TicketDto,
  TicketQuery,
  UpdateTicketInput,
  UserDto,
} from '../../types';
import { ApiError, MESSAGES } from '../http';
import { DEMO_MFA_CODE, db, nextId, persist, recordAudit, scheduleScan, type MockTicket, type MockUser } from './db';

const LATENCY_MS = 150;
const delay = () => new Promise<void>((r) => setTimeout(r, LATENCY_MS));

function base64Url(text: string): string {
  return btoa(unescape(encodeURIComponent(text))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Erfundenes Token im JWT-Format (unsigniert), damit die Oberfläche den mfa-Claim lesen kann. */
function issueToken(user: MockUser, mfa: boolean): string {
  const payload = { sub: user.id, role: user.role, team_id: user.teamId, mfa: mfa ? 'true' : 'false', jti: nextId('jti') };
  const token = `demo.${base64Url(JSON.stringify(payload))}.demo`;
  db.sessions[token] = { userId: user.id, mfa };
  return token;
}

function current(token: string | null): { user: MockUser; mfa: boolean } {
  const s = token ? db.sessions[token] : undefined;
  const user = s ? db.users.find((u) => u.id === s.userId && u.isActive) : undefined;
  if (!s || !user) throw new ApiError(401, MESSAGES.expired, 'expired');
  return { user, mfa: s.mfa };
}

function requireAdmin(user: MockUser): void {
  if (user.role !== 'Admin') throw new ApiError(403, MESSAGES.forbidden, 'forbidden');
}

function canSee(user: MockUser, teamId: string): boolean {
  return user.role === 'Admin' || user.teamId === teamId;
}

const loginResult = (accessToken: string): LoginResponseDto => ({ accessToken, expiresInSeconds: 900, mfaRequired: false, mfaToken: null });
const toProject = ({ teamId: _t, ...p }: ProjectDto & { teamId: string }): ProjectDto => p;
const toTicket = ({ teamId: _t, assigneeId: _a, ...t }: MockTicket): TicketDto => t;
const toUser = ({ password: _p, ...u }: MockUser): UserDto => u;

// ------------------------------------------------------------------ Auth --

export async function mockLogin(email: string, password: string): Promise<LoginResponseDto> {
  await delay();
  const user = db.users.find((u) => u.email.toLowerCase() === email.toLowerCase() && u.isActive);
  if (!user || user.password !== password) throw new ApiError(401, 'Anmeldung fehlgeschlagen.', 'expired');
  if (user.mfaEnabled) {
    const mfaToken = nextId('mfa');
    db.pendingMfa[mfaToken] = user.id;
    return { accessToken: null, expiresInSeconds: null, mfaRequired: true, mfaToken };
  }
  return loginResult(issueToken(user, false));
}

export async function mockVerifyMfa(mfaToken: string, code: string): Promise<LoginResponseDto> {
  await delay();
  const user = db.users.find((u) => u.id === db.pendingMfa[mfaToken]);
  if (!user || code.trim() !== DEMO_MFA_CODE) throw new ApiError(401, 'Anmeldung fehlgeschlagen.', 'expired');
  delete db.pendingMfa[mfaToken];
  return loginResult(issueToken(user, true));
}

export async function mockMe(token: string): Promise<MeDto> {
  await delay();
  const { user } = current(token);
  return { id: user.id, email: user.email, role: user.role, teamId: user.teamId, mfaEnabled: user.mfaEnabled };
}

export async function mockLogout(token: string): Promise<void> {
  await delay();
  current(token);
  delete db.sessions[token];
}

export async function mockSetupMfa(token: string): Promise<MfaSetupDto> {
  await delay();
  const { user } = current(token);
  if (user.mfaEnabled) throw new ApiError(409, MESSAGES.conflict, 'conflict');
  const secret = 'JBSWY3DPEHPK3PXP';
  const label = encodeURIComponent(`SIMS:${user.email}`);
  return { secret, otpauthUri: `otpauth://totp/${label}?secret=${secret}&issuer=SIMS&digits=6` };
}

export async function mockConfirmMfa(token: string, code: string): Promise<void> {
  await delay();
  const { user } = current(token);
  if (code.trim() !== DEMO_MFA_CODE) throw new ApiError(400, MESSAGES.input, 'input');
  user.mfaEnabled = true;
  persist();
}

// ------------------------------------------------------------- Projekte --

export async function mockListProjects(token: string): Promise<ProjectDto[]> {
  await delay();
  const { user } = current(token);
  return db.projects.filter((p) => canSee(user, p.teamId)).map(toProject);
}

export async function mockCreateProject(token: string, name: string): Promise<ProjectDto> {
  await delay();
  const { user } = current(token);
  const project = { id: nextId('p'), name, teamId: user.teamId, createdAt: new Date().toISOString() };
  db.projects.push(project);
  persist();
  return toProject(project);
}

/** Fremde und fehlende Projekte liefern beide 404 (wie B3). */
export async function mockGetProject(token: string, projectId: string): Promise<ProjectDto> {
  await delay();
  const { user } = current(token);
  const project = db.projects.find((p) => p.id === projectId);
  if (!project || !canSee(user, project.teamId)) throw new ApiError(404, MESSAGES.notfound, 'notfound');
  return toProject(project);
}

export async function mockListProjectScans(token: string, projectId: string): Promise<ScanSummaryDto[]> {
  await mockGetProject(token, projectId);
  return db.scans
    .filter((s) => s.projectId === projectId)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .map((s) => ({ id: s.id, version: s.version, status: s.status, verdict: s.verdict, createdAt: s.createdAt }));
}

export async function mockCreateScan(token: string, projectId: string, version: string, bomJson: string): Promise<ScanCreatedDto> {
  await mockGetProject(token, projectId);
  let bom: { bomFormat?: unknown; components?: unknown };
  try {
    bom = JSON.parse(bomJson) as typeof bom;
  } catch {
    throw new ApiError(400, MESSAGES.input, 'input', ['Die Datei ist kein gültiges JSON.']);
  }
  if (bom.bomFormat !== 'CycloneDX' || !Array.isArray(bom.components)) {
    throw new ApiError(400, MESSAGES.input, 'input', ['Erwartet wird CycloneDX-JSON mit components.']);
  }
  const scan: ScanDetailDto = {
    id: nextId('s'), projectId, version, status: 'Pending', verdict: null,
    errorMessage: null, createdAt: new Date().toISOString(), finishedAt: null, findings: [],
  };
  db.scans.unshift(scan);
  persist();
  scheduleScan(scan);
  return { scanId: scan.id, status: 'Pending' };
}

export async function mockGetScan(token: string, scanId: string): Promise<ScanDetailDto> {
  await delay();
  const scan = db.scans.find((s) => s.id === scanId);
  if (!scan) throw new ApiError(404, MESSAGES.notfound, 'notfound');
  await mockGetProject(token, scan.projectId);
  return { ...scan, findings: [...scan.findings] };
}

// -------------------------------------------------------------- Tickets --

function visibleTicket(token: string, ticketId: string): { user: MockUser; ticket: MockTicket } {
  const { user } = current(token);
  const ticket = db.tickets.find((t) => t.id === ticketId);
  if (!ticket || !canSee(user, ticket.teamId)) throw new ApiError(404, MESSAGES.notfound, 'notfound');
  return { user, ticket };
}

function isFinished(t: MockTicket): boolean {
  return t.status === 'Closed' || t.status === 'RiskAccepted';
}

export async function mockListTickets(token: string, query: TicketQuery): Promise<PagedDto<TicketDto>> {
  await delay();
  const { user } = current(token);
  let items = db.tickets.filter((t) => canSee(user, t.teamId));
  if (query.status) items = items.filter((t) => t.status === query.status);
  if (query.severity) items = items.filter((t) => t.severity === query.severity);
  if (query.assignedToMe) items = items.filter((t) => t.assigneeId === user.id);
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 20;
  const start = (page - 1) * pageSize;
  return { items: items.slice(start, start + pageSize).map(toTicket), page, pageSize, totalCount: items.length };
}

export async function mockGetTicket(token: string, ticketId: string): Promise<TicketDto> {
  await delay();
  return toTicket(visibleTicket(token, ticketId).ticket);
}

export async function mockCreateTicket(token: string, input: CreateTicketInput): Promise<TicketDto> {
  await delay();
  const { user } = current(token);
  let teamId = user.teamId;
  if (input.projectId) {
    const project = db.projects.find((p) => p.id === input.projectId);
    if (!project || !canSee(user, project.teamId)) throw new ApiError(404, MESSAGES.notfound, 'notfound');
    teamId = project.teamId;
  }
  const hours = { Critical: 4, High: 24, Medium: 72, Low: null }[input.severity];
  const ticket: MockTicket = {
    id: nextId('tk'), title: input.title, description: input.description, severity: input.severity, status: 'Open',
    cve: input.cve, system: input.system, projectId: input.projectId, reporterName: user.email,
    assigneeName: null, assigneeId: null, assignedRole: null, escalationLevel: 1,
    dueAt: hours === null ? null : new Date(Date.now() + hours * 3_600_000).toISOString(),
    createdAt: new Date().toISOString(), closedAt: null, teamId,
  };
  db.tickets.unshift(ticket);
  recordAudit(user.email, 'TicketCreated', ticket.id, 'Manuell angelegt [DEMO]');
  persist();
  return toTicket(ticket);
}

export async function mockUpdateTicket(token: string, ticketId: string, input: UpdateTicketInput): Promise<TicketDto> {
  await delay();
  const { user, ticket } = visibleTicket(token, ticketId);
  if (isFinished(ticket)) throw new ApiError(409, 'Das Ticket ist bereits abgeschlossen.', 'conflict');
  ticket.title = input.title;
  ticket.description = input.description;
  ticket.severity = input.severity;
  recordAudit(user.email, 'TicketUpdated', ticket.id, 'Bearbeitet [DEMO]');
  persist();
  return toTicket(ticket);
}

export async function mockTakeTicket(token: string, ticketId: string): Promise<void> {
  await delay();
  const { user, ticket } = visibleTicket(token, ticketId);
  if (isFinished(ticket)) throw new ApiError(409, 'Das Ticket ist bereits abgeschlossen.', 'conflict');
  ticket.assigneeId = user.id;
  ticket.assigneeName = user.email;
  ticket.status = 'InProgress';
  recordAudit(user.email, 'TicketTaken', ticket.id, 'Übernommen [DEMO]');
  persist();
}

export async function mockCloseTicket(token: string, ticketId: string): Promise<void> {
  await delay();
  const { user, ticket } = visibleTicket(token, ticketId);
  if (isFinished(ticket)) throw new ApiError(409, 'Das Ticket ist bereits abgeschlossen.', 'conflict');
  ticket.status = 'Closed';
  ticket.closedAt = new Date().toISOString();
  ticket.dueAt = null;
  recordAudit(user.email, 'TicketClosed', ticket.id, 'Geschlossen [DEMO]');
  persist();
}

/** Wie im Backend: AdminOnly + MfaRequired, Begründung 20 bis 2000 Zeichen. */
export async function mockAcceptRisk(token: string, ticketId: string, justification: string): Promise<void> {
  await delay();
  const { user, mfa } = current(token);
  if (user.role !== 'Admin' || !mfa) throw new ApiError(403, MESSAGES.forbidden, 'forbidden');
  const { ticket } = visibleTicket(token, ticketId);
  const len = justification.trim().length;
  if (len < 20 || len > 2000) {
    throw new ApiError(400, MESSAGES.input, 'input', ['Die Begründung muss zwischen 20 und 2000 Zeichen lang sein.']);
  }
  if (isFinished(ticket)) throw new ApiError(409, 'Das Ticket ist bereits abgeschlossen.', 'conflict');
  ticket.status = 'RiskAccepted';
  ticket.closedAt = new Date().toISOString();
  ticket.dueAt = null;
  recordAudit(user.email, 'RiskAccepted', ticket.id, `Begründung: ${justification.trim()} [DEMO]`);
  persist();
}

export async function mockListAudit(token: string, page: number, pageSize: number): Promise<PagedDto<AuditEntryDto>> {
  await delay();
  requireAdmin(current(token).user);
  const start = (page - 1) * pageSize;
  return { items: db.audit.slice(start, start + pageSize), page, pageSize, totalCount: db.audit.length };
}

// ----------------------------------------------------------- Verwaltung --

export async function mockListUsers(token: string): Promise<UserDto[]> {
  await delay();
  requireAdmin(current(token).user);
  return [...db.users].sort((a, b) => a.email.localeCompare(b.email)).map(toUser);
}

export async function mockCreateUser(token: string, input: CreateUserInput): Promise<UserDto> {
  await delay();
  const { user } = current(token);
  requireAdmin(user);
  const errors: string[] = [];
  if (!['Developer', 'Lead', 'Admin'].includes(input.role)) errors.push('Unbekannte Rolle');
  if (!db.teams.some((t) => t.id === input.teamId)) errors.push('Unbekannte TeamID');
  if (input.password.length < 12) errors.push('Passwords must be at least 12 characters.');
  if (db.users.some((u) => u.email.toLowerCase() === input.email.toLowerCase())) errors.push(`Email '${input.email}' is already taken.`);
  if (errors.length > 0) throw new ApiError(400, 'Der User konnte nicht angelegt werden.', 'input', errors);
  const created: MockUser = { id: nextId('u'), email: input.email, role: input.role, teamId: input.teamId, isActive: true, mfaEnabled: false, password: input.password };
  db.users.push(created);
  persist();
  return toUser(created);
}

export async function mockDeactivateUser(token: string, userId: string): Promise<void> {
  await delay();
  const { user } = current(token);
  requireAdmin(user);
  if (user.id === userId) throw new ApiError(400, 'Der eigene Account kann nicht deaktiviert werden.', 'input');
  const target = db.users.find((u) => u.id === userId);
  if (!target) throw new ApiError(404, MESSAGES.notfound, 'notfound');
  target.isActive = false;
  for (const [tok, s] of Object.entries(db.sessions)) if (s.userId === userId) delete db.sessions[tok];
  persist();
}

export async function mockListTeams(token: string): Promise<TeamDto[]> {
  await delay();
  requireAdmin(current(token).user);
  return [...db.teams].sort((a, b) => a.name.localeCompare(b.name));
}

export async function mockCreateTeam(token: string, name: string): Promise<TeamDto> {
  await delay();
  requireAdmin(current(token).user);
  if (db.teams.some((t) => t.name === name)) throw new ApiError(409, 'Ein Team mit diesem Namen gibt es schon.', 'conflict');
  const team: TeamDto = { id: nextId('team'), name };
  db.teams.push(team);
  persist();
  return team;
}

// ----------------------------------------------------------------- News --

export async function mockListNews(): Promise<NewsItemDto[]> {
  await delay();
  return [...db.news].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt));
}
