// API-Client für die Benutzer- und Teamverwaltung (A3/E4).
// Die Endpunkte liegen in der Auth-API: GET/POST /users,
// POST /users/{id}/deactivate, GET/POST /teams. Nur für Admins.

import { simsConfig, USE_MOCKS } from '../config';
import type { CreateUserInput, TeamDto, UserDto } from '../types';
import { request } from './http';
import * as mock from './mock/server';

const base = () => simsConfig.authUrl;

export const adminApi = {
  listUsers(token: string): Promise<UserDto[]> {
    if (USE_MOCKS) return mock.mockListUsers(token);
    return request<UserDto[]>(base(), '/users', { token });
  },

  createUser(token: string, input: CreateUserInput): Promise<UserDto> {
    if (USE_MOCKS) return mock.mockCreateUser(token, input);
    return request<UserDto>(base(), '/users', { method: 'POST', token, json: input });
  },

  deactivateUser(token: string, userId: string): Promise<void> {
    if (USE_MOCKS) return mock.mockDeactivateUser(token, userId);
    return request<void>(base(), `/users/${encodeURIComponent(userId)}/deactivate`, { method: 'POST', token });
  },

  listTeams(token: string): Promise<TeamDto[]> {
    if (USE_MOCKS) return mock.mockListTeams(token);
    return request<TeamDto[]>(base(), '/teams', { token });
  },

  createTeam(token: string, name: string): Promise<TeamDto> {
    if (USE_MOCKS) return mock.mockCreateTeam(token, name);
    return request<TeamDto>(base(), '/teams', { method: 'POST', token, json: { name } });
  },
};
