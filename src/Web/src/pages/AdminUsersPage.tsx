// E4: Admins listen Benutzer (GET /users), legen neue an (POST /users mit
// E-Mail, Passwort, Rolle und Team aus GET /teams) und deaktivieren sie
// (POST /users/{id}/deactivate). Alle Endpunkte liegen in der Auth-API.

import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';
import { adminApi } from '../api/adminApi';
import { AdminTabs } from '../components/AdminTabs';
import { EmptyState, ErrorBox, Field, Spinner } from '../components/ui';
import type { Role, TeamDto, UserDto } from '../types';

const MIN_PASSWORD = 12;

export function AdminUsersPage() {
  const { token, user: me } = useAuth();
  const [users, setUsers] = useState<UserDto[] | null>(null);
  const [teams, setTeams] = useState<TeamDto[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [actionError, setActionError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<Role>('Developer');
  const [teamId, setTeamId] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    Promise.all([adminApi.listUsers(token), adminApi.listTeams(token)])
      .then(([u, t]) => {
        setUsers(u);
        setTeams(t);
        setTeamId((current) => current || t[0]?.id || '');
      })
      .catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  const teamName = useMemo(() => {
    const map = new Map(teams.map((t) => [t.id, t.name]));
    return (id: string) => map.get(id) ?? id;
  }, [teams]);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!token) return;
    setBusy(true);
    setActionError(null);
    setNotice(null);
    try {
      const created = await adminApi.createUser(token, { email: email.trim(), password, role, teamId });
      setEmail('');
      setPassword('');
      setNotice(`${created.email} wurde angelegt.`);
      reload();
    } catch (err) {
      setActionError(err);
    } finally {
      setBusy(false);
    }
  }

  async function onDeactivate(u: UserDto) {
    if (!token) return;
    if (!window.confirm(`${u.email} wirklich deaktivieren? Die Person kann sich danach nicht mehr anmelden.`)) return;
    setActionError(null);
    setNotice(null);
    try {
      await adminApi.deactivateUser(token, u.id);
      setNotice(`${u.email} wurde deaktiviert. Ein bereits ausgestelltes Token gilt noch bis zu 15 Minuten.`);
      reload();
    } catch (err) {
      setActionError(err);
    }
  }

  return (
    <div>
      <h1>Verwaltung</h1>
      <AdminTabs />
      {error ? <ErrorBox error={error} /> : null}
      {actionError ? <ErrorBox error={actionError} /> : null}
      {notice ? <div className="noticebox" role="status">{notice}</div> : null}
      {!users && !error ? <Spinner label="Benutzer werden geladen…" /> : null}
      {users && users.length === 0 ? <EmptyState text="Keine Benutzer vorhanden." /> : null}
      {users && users.length > 0 ? (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>E-Mail</th>
                <th>Rolle</th>
                <th>Team</th>
                <th>2FA</th>
                <th>Status</th>
                <th>Aktion</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id}>
                  <td>{u.email}</td>
                  <td>{u.role}</td>
                  <td>{teamName(u.teamId)}</td>
                  <td>{u.mfaEnabled ? 'ja' : 'nein'}</td>
                  <td>{u.isActive ? 'aktiv' : 'deaktiviert'}</td>
                  <td>
                    {u.isActive && u.id !== me?.id ? (
                      <button type="button" className="danger" onClick={() => onDeactivate(u)}>
                        Deaktivieren
                      </button>
                    ) : (
                      <span className="hint">–</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      <div className="card">
        <h2>Neuen Benutzer anlegen</h2>
        <form onSubmit={onCreate}>
          <Field label="E-Mail">
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="off" />
          </Field>
          <Field label="Startpasswort" hint={`Mindestens ${MIN_PASSWORD} Zeichen. Die übrigen Regeln prüft der Server.`}>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={MIN_PASSWORD}
              autoComplete="new-password"
            />
          </Field>
          <Field label="Rolle">
            <select value={role} onChange={(e) => setRole(e.target.value as Role)}>
              <option value="Developer">Developer</option>
              <option value="Lead">Lead</option>
              <option value="Admin">Admin</option>
            </select>
          </Field>
          <Field label="Team">
            <select value={teamId} onChange={(e) => setTeamId(e.target.value)} required>
              {teams.map((t) => (
                <option key={t.id} value={t.id}>{t.name}</option>
              ))}
            </select>
          </Field>
          <div style={{ marginTop: 'var(--spacing-md)' }}>
            <button type="submit" disabled={busy || !email.trim() || password.length < MIN_PASSWORD || !teamId}>
              {busy ? 'Wird angelegt…' : 'Benutzer anlegen'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
