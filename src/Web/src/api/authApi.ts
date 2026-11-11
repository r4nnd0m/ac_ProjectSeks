// fix chwe

// API-Client für die Auth-API

import { simsConfig, USE_MOCKS } from '../config';
import type { AuthSessionDto, LoginResultDto, MfaSetupDto, SessionUserDto } from '../types';
import { request } from './http';
import * as mock from './mock/server';

const base = () => simsConfig.authUrl;

export const authApi = {
  // E-Mail/Passwort; antwortet mit Session oder mit dem Zweitschritt-Token.
  async login(email: string, password: string): Promise<LoginResultDto> {
    if (USE_MOCKS) return mock.mockLogin(email, password);
    
    // Sendet den Request an den Auth-Endpunkt und mappt sicherheitshalber 
    // sowohl camelCase als auch PascalCase auf das im Frontend erwartete Format
    const res = await request<any>(base(), '/auth/login', { method: 'POST', json: { email, password } });
    
    return {
      accessToken: res.accessToken ?? res.AccessToken,
      expiresInSeconds: res.expiresInSeconds ?? res.ExpiresInSeconds ?? 900,
      mfaRequired: res.mfaRequired ?? res.MfaRequired ?? false,
      mfaToken: res.mfaToken ?? res.MfaToken ?? null,
    } as LoginResultDto;
  },

  // Zweiter Anmeldeschritt mit dem Einmalcode (E2). 
  verifyMfa(mfaToken: string, code: string): Promise<AuthSessionDto> {
    if (USE_MOCKS) return mock.mockVerifyMfa(mfaToken, code);
    return request<AuthSessionDto>(base(), '/auth/mfa/verify', { method: 'POST', json: { mfaToken, code } });
  },

  // Aktuelle Person hinter einem Token (für Sitzungsprüfung nach Reload).
  me(token: string): Promise<SessionUserDto> {
    if (USE_MOCKS) return mock.mockMe(token);
    return request<SessionUserDto>(base(), '/auth/me', { token });
  },

  // Einrichtung der Zwei-Faktor-Anmeldung: Secret + otpauth-URL (E2).
  setupMfa(token: string): Promise<MfaSetupDto> {
    if (USE_MOCKS) return mock.mockSetupMfa(token);
    return request<MfaSetupDto>(base(), '/auth/mfa/setup', { method: 'POST', token });
  },

  confirmMfa(token: string, code: string): Promise<void> {
    if (USE_MOCKS) return mock.mockConfirmMfa(token, code);
    return request<void>(base(), '/auth/mfa/confirm', { method: 'POST', token, json: { code } });
  },
};




// // API-Client für die Auth-API (Port 8081).
// //
// // PROVISORISCH: Die Auth-API hat im Repo noch keine Controller und es gibt
// // keine OpenAPI-Beschreibung. Die folgenden Pfade sind aus der Projektidee
// // abgeleitet und müssen mit dem Backend abgestimmt werden:
// //   POST /auth/login, POST /auth/mfa/verify, GET /auth/me,
// //   POST /auth/mfa/setup, POST /auth/mfa/confirm
// // Siehe Seite „Hinweise“ in der Oberfläche.

// import { simsConfig, USE_MOCKS } from '../config';
// import type { AuthSessionDto, LoginResultDto, MfaSetupDto, SessionUserDto } from '../types';
// import { request } from './http';
// import * as mock from './mock/server';

// const base = () => simsConfig.authUrl;

// export const authApi = {
//   /** E-Mail/Passwort; antwortet mit Session oder mit dem Zweitschritt-Token. */
//   login(email: string, password: string): Promise<LoginResultDto> {
//     if (USE_MOCKS) return mock.mockLogin(email, password);
//     return request<LoginResultDto>(base(), '/auth/login', { method: 'POST', json: { email, password } });
//   },

//   /** Zweiter Anmeldeschritt mit dem Einmalcode (E2). */
//   verifyMfa(mfaToken: string, code: string): Promise<AuthSessionDto> {
//     if (USE_MOCKS) return mock.mockVerifyMfa(mfaToken, code);
//     return request<AuthSessionDto>(base(), '/auth/mfa/verify', { method: 'POST', json: { mfaToken, code } });
//   },

//   /** Aktuelle Person hinter einem Token (für Sitzungsprüfung nach Reload). */
//   me(token: string): Promise<SessionUserDto> {
//     if (USE_MOCKS) return mock.mockMe(token);
//     return request<SessionUserDto>(base(), '/auth/me', { token });
//   },

//   /** Einrichtung der Zwei-Faktor-Anmeldung: Secret + otpauth-URL (E2). */
//   setupMfa(token: string): Promise<MfaSetupDto> {
//     if (USE_MOCKS) return mock.mockSetupMfa(token);
//     return request<MfaSetupDto>(base(), '/auth/mfa/setup', { method: 'POST', token });
//   },

//   confirmMfa(token: string, code: string): Promise<void> {
//     if (USE_MOCKS) return mock.mockConfirmMfa(token, code);
//     return request<void>(base(), '/auth/mfa/confirm', { method: 'POST', token, json: { code } });
//   },
// };
