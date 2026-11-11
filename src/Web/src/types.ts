// Zentrale Typen der Weboberfläche (E1).
//
// Die Typen bilden 1:1 die Antworten der echten APIs ab (camelCase, wie
// ASP.NET Core sie serialisiert). Quelle sind die DTOs im Backend:
//   AuthApi/Dtos (LoginResponse, MeResponse, MfaSetupResponse, UserResponse, TeamResponse)
//   ScanApi/Dtos + ScanApi/Models (ProjectResponse, Scan*, TicketResponse, AuditEntryResponse, PagedResponse)
//   NewsApi: NewsItemResponse(Title, Link, Source, PublishedAt) laut N2.
// Sobald die OpenAPI-Dateien unter api-specs/ liegen, können diese Typen mit
// openapi-typescript erzeugt und hier ersetzt werden (siehe README im Ordner src/Web).

export type ScanStatus = 'Pending' | 'Scanning' | 'Scanned' | 'Failed';
export type Verdict = 'Go' | 'GoWithConditions' | 'NoGo';
export type Severity = 'Critical' | 'High' | 'Medium' | 'Low';
export type TicketStatus = 'Open' | 'InProgress' | 'Escalated' | 'Closed' | 'RiskAccepted';
/** Rollen aus Sims.Contracts/Roles.cs */
export type Role = 'Developer' | 'Lead' | 'Admin';

// ------------------------------------------------------------------ Auth ---

/** POST /auth/login und POST /auth/mfa/verify */
export interface LoginResponseDto {
  accessToken: string | null;
  expiresInSeconds: number | null;
  mfaRequired: boolean;
  mfaToken: string | null;
}

/** GET /auth/me */
export interface MeDto {
  id: string;
  email: string;
  role: Role;
  teamId: string;
  mfaEnabled: boolean;
}

/** Angemeldete Person in der Oberfläche: /auth/me plus mfa-Claim aus dem Token. */
export interface SessionUser extends MeDto {
  /** true, wenn das Token den Claim mfa=true trägt (nur Anzeige, Prüfung im Backend). */
  mfaVerified: boolean;
}

/** POST /auth/mfa/setup */
export interface MfaSetupDto {
  secret: string;
  otpauthUri: string;
}

/** GET /users (A3) */
export interface UserDto {
  id: string;
  email: string;
  role: Role;
  teamId: string;
  isActive: boolean;
  mfaEnabled: boolean;
}

/** POST /users (A3) */
export interface CreateUserInput {
  email: string;
  password: string;
  role: Role;
  teamId: string;
}

/** GET /teams (A3) */
export interface TeamDto {
  id: string;
  name: string;
}

// ------------------------------------------------------------------ Scan ---

export interface ProjectDto {
  id: string;
  name: string;
  createdAt: string;
}

/** Antwort auf POST /projects/{id}/scans (HTTP 202) */
export interface ScanCreatedDto {
  scanId: string;
  status: ScanStatus;
}

/** GET /projects/{id}/scans */
export interface ScanSummaryDto {
  id: string;
  version: string;
  status: ScanStatus;
  verdict: Verdict | null;
  createdAt: string;
}

export interface FindingDto {
  id: string;
  component: string;
  componentVersion: string;
  vulnId: string;
  cve: string | null;
  severity: Severity;
  isKev: boolean;
  /** EPSS 0..1 oder null */
  epss: number | null;
}

/** GET /scans/{id} */
export interface ScanDetailDto {
  id: string;
  projectId: string;
  version: string;
  status: ScanStatus;
  verdict: Verdict | null;
  errorMessage: string | null;
  createdAt: string;
  finishedAt: string | null;
  findings: FindingDto[];
}

/** TicketResponse – Liste und Detail haben dieselbe Form. */
export interface TicketDto {
  id: string;
  title: string;
  description: string;
  severity: Severity;
  status: TicketStatus;
  cve: string | null;
  system: string | null;
  projectId: string | null;
  reporterName: string;
  assigneeName: string | null;
  assignedRole: string | null;
  escalationLevel: number;
  dueAt: string | null;
  createdAt: string;
  closedAt: string | null;
}

export interface CreateTicketInput {
  title: string;
  description: string;
  severity: Severity;
  cve: string | null;
  system: string | null;
  projectId: string | null;
}

export interface UpdateTicketInput {
  title: string;
  description: string;
  severity: Severity;
}

/** Filter für GET /tickets (TicketFilter im Backend). Paging beginnt bei 1. */
export interface TicketQuery {
  status?: TicketStatus;
  severity?: Severity;
  assignedToMe?: boolean;
  page?: number;
  pageSize?: number;
}

/** GET /audit (paged) */
export interface AuditEntryDto {
  id: string;
  timestamp: string;
  userName: string;
  action: string;
  ticketId: string | null;
  details: string | null;
}

export interface PagedDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// ------------------------------------------------------------------ News ---

/** GET /news (N2: NewsItemResponse) */
export interface NewsItemDto {
  title: string;
  link: string;
  source: string;
  publishedAt: string;
}
