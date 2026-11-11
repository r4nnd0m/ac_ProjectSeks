// Hält die Anmeldung (E2). Das Zugriffstoken liegt nur im Arbeitsspeicher
// (React-State): kein localStorage, kein sessionStorage. Nach einem Neuladen
// ist man abgemeldet – das ist gewollt.
//
// Ablauf: POST /auth/login -> entweder accessToken oder mfaToken.
// Mit mfaToken folgt POST /auth/mfa/verify. Danach liefert GET /auth/me Rolle
// und Team (nur Anzeige, die Rechte prüft der Server).

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../api/authApi';
import { setUnauthorizedHandler } from '../api/http';
import type { SessionUser } from '../types';
import { hasMfaClaim } from './token';

interface AuthState {
  token: string | null;
  user: SessionUser | null;
  /** Token aus dem ersten Anmeldeschritt, solange der Einmalcode fehlt. */
  pendingMfaToken: string | null;
  login(email: string, password: string): Promise<{ mfaRequired: boolean }>;
  verifyMfa(code: string): Promise<void>;
  /** Sperrt das Token serverseitig und verwirft es lokal. */
  logout(): Promise<void>;
  /** Nach bestätigter 2FA-Einrichtung (Anzeige). */
  markMfaEnabled(): void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<SessionUser | null>(null);
  const [pendingMfaToken, setPendingMfaToken] = useState<string | null>(null);

  const clear = useCallback(() => {
    setToken(null);
    setUser(null);
    setPendingMfaToken(null);
  }, []);

  // 401 bei einer Anfrage mit Token -> Sitzung verwerfen, RequireAuth leitet zu /login.
  useEffect(() => {
    setUnauthorizedHandler(clear);
    return () => setUnauthorizedHandler(null);
  }, [clear]);

  const establish = useCallback(async (accessToken: string) => {
    const me = await authApi.me(accessToken);
    setToken(accessToken);
    setUser({ ...me, mfaVerified: hasMfaClaim(accessToken) });
    setPendingMfaToken(null);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const result = await authApi.login(email, password);
    if (result.mfaRequired && result.mfaToken) {
      setPendingMfaToken(result.mfaToken);
      return { mfaRequired: true };
    }
    if (!result.accessToken) throw new Error('Die Anmeldung lieferte kein Token.');
    await establish(result.accessToken);
    return { mfaRequired: false };
  }, [establish]);

  const verifyMfa = useCallback(async (code: string) => {
    if (!pendingMfaToken) throw new Error('Kein offener Anmeldeschritt. Bitte neu anmelden.');
    const result = await authApi.verifyMfa(pendingMfaToken, code.trim());
    if (!result.accessToken) throw new Error('Die Bestätigung lieferte kein Token.');
    await establish(result.accessToken);
  }, [pendingMfaToken, establish]);

  const logout = useCallback(async () => {
    const current = token;
    clear();
    if (current) {
      try {
        await authApi.logout(current);
      } catch {
        /* Token ist lokal verworfen; ein Fehler beim Sperren ändert daran nichts. */
      }
    }
  }, [token, clear]);

  const markMfaEnabled = useCallback(() => {
    setUser((u) => (u ? { ...u, mfaEnabled: true } : u));
  }, []);

  const value = useMemo(
    () => ({ token, user, pendingMfaToken, login, verifyMfa, logout, markMfaEnabled }),
    [token, user, pendingMfaToken, login, verifyMfa, logout, markMfaEnabled],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth muss innerhalb von <AuthProvider> benutzt werden.');
  return ctx;
}
