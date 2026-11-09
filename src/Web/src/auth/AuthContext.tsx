// Hält die Anmeldung. Das Zugriffstoken liegt nur im Arbeitsspeicher
// (Auftrag E2): kein sessionStorage, kein localStorage. Ein Neuladen des
// Browsers beendet die Sitzung bewusst – das ist die sicherere Variante.

import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../api/authApi';
import type { AuthSessionDto, LoginResultDto, SessionUserDto } from '../types';

interface AuthState {
  token: string | null;
  user: SessionUserDto | null;
  /** Token aus dem ersten Anmeldeschritt, solange der Einmalcode fehlt. */
  pendingMfaToken: string | null;
  login(email: string, password: string): Promise<LoginResultDto>;
  verifyMfa(code: string): Promise<void>;
  logout(): void;
  /** Nach erfolgreich bestätigter 2FA-Einrichtung. */
  markMfaEnabled(): void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<SessionUserDto | null>(null);
  const [pendingMfaToken, setPendingMfaToken] = useState<string | null>(null);

  const applySession = useCallback((session: AuthSessionDto) => {
    setToken(session.token);
    setUser(session.user);
    setPendingMfaToken(null);
  }, []);

  const login = useCallback(async (email: string, password: string): Promise<LoginResultDto> => {
    const result = await authApi.login(email, password);
    if (result.mfaRequired) {
      setPendingMfaToken(result.mfaToken);
    } else {
      applySession(result.session);
    }
    return result;
  }, [applySession]);

  const verifyMfa = useCallback(async (code: string) => {
    if (!pendingMfaToken) throw new Error('Kein offener Anmeldeschritt. Bitte neu anmelden.');
    const session = await authApi.verifyMfa(pendingMfaToken, code);
    applySession(session);
  }, [pendingMfaToken, applySession]);

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    setPendingMfaToken(null);
  }, []);

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
