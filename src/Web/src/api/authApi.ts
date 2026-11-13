// API-Client für die Auth-API: Anmeldung, MFA, Abmeldung (E2).
// Endpunkte: POST /auth/login, POST /auth/mfa/verify, GET /auth/me,
// POST /auth/logout, POST /auth/mfa/setup, POST /auth/mfa/confirm.

import { simsConfig, USE_MOCKS } from '../config';
import type { LoginResponseDto, MeDto, MfaSetupDto } from '../types';
import { request } from './http';
import * as mock from './mock/server';

const base = () => simsConfig.authUrl;

export const authApi = {
  login(email: string, password: string): Promise<LoginResponseDto> {
    if (USE_MOCKS) return mock.mockLogin(email, password);
    return request<LoginResponseDto>(base(), '/auth/login', { method: 'POST', json: { email, password } });
  },

  /** Zweiter Schritt: liefert dieselbe Antwortform wie /auth/login, jetzt mit accessToken. */
  verifyMfa(mfaToken: string, code: string): Promise<LoginResponseDto> {
    if (USE_MOCKS) return mock.mockVerifyMfa(mfaToken, code);
    return request<LoginResponseDto>(base(), '/auth/mfa/verify', { method: 'POST', json: { mfaToken, code } });
  },

  me(token: string): Promise<MeDto> {
    if (USE_MOCKS) return mock.mockMe(token);
    return request<MeDto>(base(), '/auth/me', { token });
  },

  /** Sperrt das Token serverseitig (Redis-Sperrliste). */
  logout(token: string): Promise<void> {
    if (USE_MOCKS) return mock.mockLogout(token);
    return request<void>(base(), '/auth/logout', { method: 'POST', token });
  },

  setupMfa(token: string): Promise<MfaSetupDto> {
    if (USE_MOCKS) return mock.mockSetupMfa(token);
    return request<MfaSetupDto>(base(), '/auth/mfa/setup', { method: 'POST', token });
  },

  confirmMfa(token: string, code: string): Promise<void> {
    if (USE_MOCKS) return mock.mockConfirmMfa(token, code);
    return request<void>(base(), '/auth/mfa/confirm', { method: 'POST', token, json: { code } });
  },
};
