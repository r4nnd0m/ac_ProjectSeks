// API-Client für die Scan-API (Port 8082): Projekte, Scans, Tickets,
// Benutzerverwaltung, Teams und Audit.
//
// Durch die Abstimmungs-Dokumente BEKANNT (docs/worker/abstimmung):
//   POST /projects/{id}/scans?version=…   (B2, liefert scanId, HTTP 202)
//   GET  /scans/{id}                      (B5: status, verdict, finishedAt,
//                                          errorMessage, findings)
//   GET  /tickets/{id}                    (C2: status, escalationLevel,
//                                          assignedRole, dueAt, assigneeName)
//   POST /tickets/{id}/take               (C2: persönliche Übernahme)
// PROVISORISCH (in den Dokumenten noch nicht festgelegt, siehe Hinweise):
//   GET/POST /projects, GET /projects/{id}, GET /projects/{id}/scans,
//   GET/POST /tickets, PUT /tickets/{id}, POST /tickets/{id}/close,
//   POST /tickets/{id}/accept-risk, /users, /teams, /audit

import { simsConfig, USE_MOCKS } from '../config';
import type {
  AuditEntryDto,
  PagedDto,
  ProjectDto,
  ScanDetailDto,
  ScanListItemDto,
  TeamDto,
  TicketDetailDto,
  TicketListItemDto,
  TicketQuery,
  UserDto,
} from '../types';
import { request } from './http';
import * as mock from './mock/server';

const base = () => simsConfig.scanUrl;

export const scanApi = {
  // ------------------------------------------------------------ Projekte --

  listProjects(token: string): Promise<ProjectDto[]> {
    if (USE_MOCKS) return mock.mockListProjects(token);
    return request<ProjectDto[]>(base(), '/projects', { token });
  },

  createProject(token: string, name: string): Promise<ProjectDto> {
    if (USE_MOCKS) return mock.mockCreateProject(token, name);
    return request<ProjectDto>(base(), '/projects', { method: 'POST', token, json: { name } });
  },

  getProject(token: string, projectId: string): Promise<ProjectDto> {
    if (USE_MOCKS) return mock.mockGetProject(token, projectId);
    return request<ProjectDto>(base(), `/projects/${encodeURIComponent(projectId)}`, { token });
  },

  listProjectScans(token: string, projectId: string): Promise<ScanListItemDto[]> {
    if (USE_MOCKS) return mock.mockListProjectScans(token, projectId);
    return request<ScanListItemDto[]>(base(), `/projects/${encodeURIComponent(projectId)}/scans`, { token });
  },

  /**
   * B2: SBOM-Upload. Das CycloneDX-JSON geht unverändert als Body an
   * POST /projects/{id}/scans?version=…; die Antwort enthält die scanId.
   */
  createScan(token: string, projectId: string, version: string, bomJson: string): Promise<ScanListItemDto> {
    if (USE_MOCKS) return mock.mockCreateScan(token, projectId, version, bomJson);
    return request<ScanListItemDto>(base(), `/projects/${encodeURIComponent(projectId)}/scans`, {
      method: 'POST',
      token,
      query: { version },
      textBody: bomJson,
      contentType: 'application/json',
    });
  },

  // --------------------------------------------------------------- Scans --

  getScan(token: string, scanId: string): Promise<ScanDetailDto> {
    if (USE_MOCKS) return mock.mockGetScan(token, scanId);
    return request<ScanDetailDto>(base(), `/scans/${encodeURIComponent(scanId)}`, { token });
  },

  // ------------------------------------------------------------- Tickets --

  listTickets(token: string, query: TicketQuery): Promise<PagedDto<TicketListItemDto>> {
    if (USE_MOCKS) return mock.mockListTickets(token, query);
    return request<PagedDto<TicketListItemDto>>(base(), '/tickets', {
      token,
      query: {
        status: query.status,
        severity: query.severity,
        mine: query.mine ? 'true' : undefined,
        page: query.page,
        pageSize: query.pageSize,
      },
    });
  },

  getTicket(token: string, ticketId: string): Promise<TicketDetailDto> {
    if (USE_MOCKS) return mock.mockGetTicket(token, ticketId);
    return request<TicketDetailDto>(base(), `/tickets/${encodeURIComponent(ticketId)}`, { token });
  },

  createTicket(
    token: string,
    input: { title: string; description: string; projectId: string; severity: TicketDetailDto['severity'] },
  ): Promise<TicketDetailDto> {
    if (USE_MOCKS) return mock.mockCreateTicket(token, input);
    return request<TicketDetailDto>(base(), '/tickets', { method: 'POST', token, json: input });
  },

  updateTicket(
    token: string,
    ticketId: string,
    patch: { title?: string; description?: string },
  ): Promise<TicketDetailDto> {
    if (USE_MOCKS) return mock.mockUpdateTicket(token, ticketId, patch);
    return request<TicketDetailDto>(base(), `/tickets/${encodeURIComponent(ticketId)}`, {
      method: 'PUT',
      token,
      json: patch,
    });
  },

  takeTicket(token: string, ticketId: string): Promise<TicketDetailDto> {
    if (USE_MOCKS) return mock.mockTakeTicket(token, ticketId);
    return request<TicketDetailDto>(base(), `/tickets/${encodeURIComponent(ticketId)}/take`, {
      method: 'POST',
      token,
    });
  },

  closeTicket(token: string, ticketId: string): Promise<TicketDetailDto> {
    if (USE_MOCKS) return mock.mockCloseTicket(token, ticketId);
    return request<TicketDetailDto>(base(), `/tickets/${encodeURIComponent(ticketId)}/close`, {
      method: 'POST',
      token,
    });
  },

  /** E4: nur Admin mit zweitem Faktor, reason mindestens 20 Zeichen. */
  acceptRisk(token: string, ticketId: string, reason: string): Promise<TicketDetailDto> {
    if (USE_MOCKS) return mock.mockAcceptRisk(token, ticketId, reason);
    return request<TicketDetailDto>(base(), `/tickets/${encodeURIComponent(ticketId)}/accept-risk`, {
      method: 'POST',
      token,
      json: { reason },
    });
  },

  // --------------------------------------------------------- Verwaltung ---

  listUsers(token: string): Promise<UserDto[]> {
    if (USE_MOCKS) return mock.mockListUsers(token);
    return request<UserDto[]>(base(), '/users', { token });
  },

  createUser(
    token: string,
    input: { email: string; name: string; role: UserDto['role']; teamId: string },
  ): Promise<UserDto> {
    if (USE_MOCKS) return mock.mockCreateUser(token, input);
    return request<UserDto>(base(), '/users', { method: 'POST', token, json: input });
  },

  disableUser(token: string, userId: string): Promise<void> {
    if (USE_MOCKS) return mock.mockDisableUser(token, userId);
    return request<void>(base(), `/users/${encodeURIComponent(userId)}/disable`, { method: 'POST', token });
  },

  listTeams(token: string): Promise<TeamDto[]> {
    if (USE_MOCKS) return mock.mockListTeams(token);
    return request<TeamDto[]>(base(), '/teams', { token });
  },

  createTeam(token: string, name: string): Promise<TeamDto> {
    if (USE_MOCKS) return mock.mockCreateTeam(token, name);
    return request<TeamDto>(base(), '/teams', { method: 'POST', token, json: { name } });
  },

  listAudit(token: string): Promise<AuditEntryDto[]> {
    if (USE_MOCKS) return mock.mockListAudit(token);
    return request<AuditEntryDto[]>(base(), '/audit', { token });
  },
};
