// Zentrale Typen der Weboberfläche.
//
// PROVISORISCH: Im Repo existieren noch keine OpenAPI-Beschreibungen
// (ScanApi/AuthApi/NewsApi sind Gerüste ohne Controller). Diese Typen wurden
// aus den Abstimmungs-Dokumenten in docs/worker/abstimmung abgeleitet:
//   B2 (Scan-Upload), B4 (Urteil), B5 (Scan-Ergebnis), C1/C2 (Tickets),
//   C4 (Eskalation), C5 (Audit), Sims.Contracts (Rollen/Claims).
// Sobald die OpenAPI-Dateien vorliegen, müssen diese Typen gegen sie
// ausgetauscht bzw. generiert werden (siehe Seite „Hinweise“).

export type ScanStatus = 'Pending' | 'Scanning' | 'Scanned' | 'Failed';

/** Urteil aus B4. Leer (null), solange kein Scanned vorliegt. */
export type Verdict = 'Go' | 'GoWithConditions' | 'NoGo';

export type Severity = 'Critical' | 'High' | 'Medium' | 'Low';

/**
 * PROVISORISCH: Statusmenge aus C1/C4 abgeleitet.
 * 'InProgress' (übernommen) ist in den Dokumenten nicht abschließend benannt.
 */
export type TicketStatus = 'Open' | 'InProgress' | 'Escalated' | 'Closed' | 'RiskAccepted';

/** Rollen aus Sims.Contracts/Roles.cs */
export type Role = 'Developer' | 'Lead' | 'Admin';

export interface ProjectDto {
  id: string;
  name: string;
  teamId: string;
  teamName: string;
  createdAt: string;
}

export interface ScanListItemDto {
  id: string;
  projectId: string;
  version: string;
  status: ScanStatus;
  /** null bei Pending/Scanning/Failed (B4/B5). */
  verdict: Verdict | null;
  createdAt: string;
  finishedAt: string | null;
}

export interface FindingDto {
  id: string;
  componentId: string;
  componentName: string;
  componentVersion: string;
  /** Schwachstellen-ID der Quelle, z. B. OSV oder GHSA (B1). */
  vulnId: string;
  cve: string | null;
  severity: Severity;
  isKev: boolean;
  /** EPSS-Wert 0..1 oder null, wenn nicht bekannt (B4). */
  epss: number | null;
}

export interface ScanDetailDto extends ScanListItemDto {
  errorMessage: string | null;
  findings: FindingDto[];
}

export interface TicketListItemDto {
  id: string;
  title: string;
  severity: Severity;
  status: TicketStatus;
  /** Eskalationsstufe 1..3 (C4). */
  escalationLevel: number;
  /** Zuständige Rolle am Ticket, unabhängig vom persönlichen Bearbeiter (C1/C2). */
  assignedRole: Role | null;
  /** Frist oder null (Stufe 3 / Low haben keine Frist). */
  dueAt: string | null;
  /** Persönlicher Bearbeiter, null wenn niemand übernommen hat (C2). */
  assigneeName: string | null;
  teamId: string;
  projectId: string;
  createdAt: string;
}

export interface AuditEntryDto {
  id: string;
  userId: string;
  userName: string;
  action: string;
  ticketId: string | null;
  details: string;
  timestamp: string;
}

export interface TicketDetailDto extends TicketListItemDto {
  description: string | null;
  scanId: string | null;
  findingId: string | null;
  updatedAt: string;
  /** PROVISORISCH: Audit-Protokoll des Tickets (C5), Feldname noch offen. */
  audit: AuditEntryDto[];
}

export interface UserDto {
  id: string;
  email: string;
  name: string;
  role: Role;
  teamId: string;
  teamName: string;
  active: boolean;
  mfaEnabled: boolean;
}

export interface TeamDto {
  id: string;
  name: string;
}

export interface NewsItemDto {
  id: string;
  title: string;
  source: string;
  publishedAt: string;
  url: string;
  /** Fremder Inhalt: wird in der Oberfläche immer als reiner Text ausgegeben. */
  summary: string | null;
}

export interface SessionUserDto {
  id: string;
  email: string;
  name: string;
  role: Role;
  teamId: string;
  /** true, wenn diese Anmeldung mit zweitem Faktor bestätigt ist (Claim 'mfa'). */
  mfaVerified: boolean;
  mfaEnabled: boolean;
}

export interface AuthSessionDto {
  token: string;
  user: SessionUserDto;
}

/** Antwort von POST /auth/login: entweder Sofort-Session oder MFA-Zweitschritt. */
export type LoginResultDto =
  | { mfaRequired: false; session: AuthSessionDto }
  | { mfaRequired: true; /** kurzes Token nur für den zweiten Anmeldeschritt */ mfaToken: string };

export interface MfaSetupDto {
  secret: string;
  /** otpauth://-URL; die Oberfläche daraus den QR-Code. */
  otpauthUrl: string;
}

export interface PagedDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TicketQuery {
  status?: TicketStatus;
  severity?: Severity;
  /** Nur Tickets mit mir als persönlichem Bearbeiter. */
  mine?: boolean;
  page?: number;
  pageSize?: number;
}
