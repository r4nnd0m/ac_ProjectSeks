// E4: Admins können Benutzer anzeigen, anlegen und deaktivieren.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { AdminTabs } from '../components/AdminTabs';
import { EmptyState, ErrorBox, Field, Spinner } from '../components/ui';
import type { Role, TeamDto, UserDto } from '../types';

export function AdminUsersPage() {
  const { token } = useAuth();
  const [users, setUsers] = useState<UserDto[] | null>(null);
  const [teams, setTeams] = useState<TeamDto[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [actionError, setActionError] = useState<unknown>(null);

  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [role, setRole] = useState<Role>('Developer');
  const [teamId, setTeamId] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    Promise.all([scanApi.listUsers(token), scanApi.listTeams(token)])
      .then(([u, t]) => {
        setUsers(u);
        setTeams(t);
        if (!teamId && t[0]) setTeamId(t[0].id);
      })
      .catch(setError);
  }, [token, teamId]);

  useEffect(reload, [reload]);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!token) return;
    setBusy(true);
    setActionError(null);
    try {
      await scanApi.createUser(token, { email: email.trim(), name: name.trim(), role, teamId });
      setEmail('');
      setName('');
      reload();
    } catch (err) {
      setActionError(err);
    } finally {
      setBusy(false);
    }
  }

  async function onDisable(userId: string) {
    if (!token) return;
    setActionError(null);
    try {
      await scanApi.disableUser(token, userId);
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
      {!users && !error ? <Spinner label="Benutzer werden geladen…" /> : null}
      {users && users.length === 0 ? <EmptyState text="Keine Benutzer vorhanden." /> : null}
      {users && users.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>Name</th>
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
                <td>{u.name}</td>
                <td>{u.email}</td>
                <td>{u.role}</td>
                <td>{u.teamName}</td>
                <td>{u.mfaEnabled ? 'ja' : 'nein'}</td>
                <td>{u.active ? 'aktiv' : 'deaktiviert'}</td>
                <td>
                  {u.active ? (
                    <button type="button" className="danger" onClick={() => onDisable(u.id)}>
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
      ) : null}

      <div className="card">
        <h2>Neuen Benutzer anlegen</h2>
        <form onSubmit={onCreate}>
          <Field label="Name">
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
          </Field>
          <Field label="E-Mail">
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
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
          <button type="submit" disabled={busy || !email.trim() || !name.trim() || !teamId}>
            {busy ? 'Wird angelegt…' : 'Benutzer anlegen'}
          </button>
        </form>
      </div>
    </div>
  );
}
