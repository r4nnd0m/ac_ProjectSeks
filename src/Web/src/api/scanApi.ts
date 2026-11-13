// API-Client für die Scan-API: Projekte, Scans, Tickets und Audit (E3/E4).
// Routen laut ScanApi/Controllers:
//   GET/POST /projects, GET /projects/{id}
//   POST /projects/{id}/scans?version=…  (202 + scanId), GET /projects/{id}/scans
//   GET /scans/{id}
//   GET/POST /tickets, GET/PUT /tickets/{id}
//   POST /tickets/{id}/take | /close | /accept-risk  (204, danach neu laden)
//   GET /audit (paged, nur Admin)

import { simsConfig, USE_MOCKS } from '../config';
import type {
  AuditEntryDto,
  CreateTicketInput,
  PagedDto,
  ProjectDto,
  ScanCreatedDto,
  ScanDetailDto,
  ScanSummaryDto,
  TicketDto,
  TicketQuery,
  UpdateTicketInput,
} from '../types';
import { request } from './http';
import * as mock from './mock/server';

const base = () => simsConfig.scanUrl;
const enc = encodeURIComponent;

// Die Scan-API liefert höchstens 100 Einträge pro Seite.
const MAX_PAGE_SIZE = 100;

async function loadAllPages<T>(load: (page: number) => Promise<PagedDto<T>>): Promise<PagedDto<T>> {
  const items: T[] = [];
  let page = 1;
  let total = 0;
  do {
    const res = await load(page);
    items.push(...res.items);
    total = res.totalCount;
    if (res.items.length === 0) break;
    page++;
  } while (items.length < total);
  return { items, page: 1, pageSize: items.length, totalCount: total };
}

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
    return request<ProjectDto>(base(), `/projects/${enc(projectId)}`, { token });
  },

  listProjectScans(token: string, projectId: string): Promise<ScanSummaryDto[]> {
    if (USE_MOCKS) return mock.mockListProjectScans(token, projectId);
    return request<ScanSummaryDto[]>(base(), `/projects/${enc(projectId)}/scans`, { token });
  },

  /** B2: Das CycloneDX-JSON geht unverändert als Body an die API. */
  createScan(token: string, projectId: string, version: string, bomJson: string): Promise<ScanCreatedDto> {
    if (USE_MOCKS) return mock.mockCreateScan(token, projectId, version, bomJson);
    return request<ScanCreatedDto>(base(), `/projects/${enc(projectId)}/scans`, {
      method: 'POST',
      token,
      query: { version },
      textBody: bomJson,
      contentType: 'application/json',
    });
  },

  getScan(token: string, scanId: string): Promise<ScanDetailDto> {
    if (USE_MOCKS) return mock.mockGetScan(token, scanId);
    return request<ScanDetailDto>(base(), `/scans/${enc(scanId)}`, { token });
  },

  // ------------------------------------------------------------- Tickets --

  listTickets(token: string, query: TicketQuery): Promise<PagedDto<TicketDto>> {
    if (USE_MOCKS) return mock.mockListTickets(token, query);
    return request<PagedDto<TicketDto>>(base(), '/tickets', {
      token,
      query: {
        status: query.status,
        severity: query.severity,
        assignedToMe: query.assignedToMe ? 'true' : undefined,
        page: query.page,
        pageSize: query.pageSize,
      },
    });
  },

  listAllTickets(token: string, query: Omit<TicketQuery, 'page' | 'pageSize'> = {}): Promise<PagedDto<TicketDto>> {
    return loadAllPages((page) => scanApi.listTickets(token, { ...query, page, pageSize: MAX_PAGE_SIZE }));
  },

  getTicket(token: string, ticketId: string): Promise<TicketDto> {
    if (USE_MOCKS) return mock.mockGetTicket(token, ticketId);
    return request<TicketDto>(base(), `/tickets/${enc(ticketId)}`, { token });
  },

  createTicket(token: string, input: CreateTicketInput): Promise<TicketDto> {
    if (USE_MOCKS) return mock.mockCreateTicket(token, input);
    return request<TicketDto>(base(), '/tickets', { method: 'POST', token, json: input });
  },

  updateTicket(token: string, ticketId: string, input: UpdateTicketInput): Promise<TicketDto> {
    if (USE_MOCKS) return mock.mockUpdateTicket(token, ticketId, input);
    return request<TicketDto>(base(), `/tickets/${enc(ticketId)}`, { method: 'PUT', token, json: input });
  },

  takeTicket(token: string, ticketId: string): Promise<void> {
    if (USE_MOCKS) return mock.mockTakeTicket(token, ticketId);
    return request<void>(base(), `/tickets/${enc(ticketId)}/take`, { method: 'POST', token });
  },

  closeTicket(token: string, ticketId: string): Promise<void> {
    if (USE_MOCKS) return mock.mockCloseTicket(token, ticketId);
    return request<void>(base(), `/tickets/${enc(ticketId)}/close`, { method: 'POST', token });
  },

  /** Nur Admin mit MFA-Anmeldung; Begründung mindestens 20 Zeichen (AcceptRiskRequest.Justification). */
  acceptRisk(token: string, ticketId: string, justification: string): Promise<void> {
    if (USE_MOCKS) return mock.mockAcceptRisk(token, ticketId, justification);
    return request<void>(base(), `/tickets/${enc(ticketId)}/accept-risk`, {
      method: 'POST',
      token,
      json: { justification },
    });
  },

  // --------------------------------------------------------------- Audit --

  listAudit(token: string, page = 1, pageSize = 20): Promise<PagedDto<AuditEntryDto>> {
    if (USE_MOCKS) return mock.mockListAudit(token, page, pageSize);
    return request<PagedDto<AuditEntryDto>>(base(), '/audit', { token, query: { page, pageSize } });
  },

  listAllAudit(token: string): Promise<PagedDto<AuditEntryDto>> {
    return loadAllPages((page) => scanApi.listAudit(token, page, MAX_PAGE_SIZE));
  },
};
