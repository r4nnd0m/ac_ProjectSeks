// E4: Admins listen Benutzer (GET /users), legen neue an (POST /users mit
// E-Mail, Passwort, Rolle und Team aus GET /teams) und deaktivieren sie
// (POST /users/{id}/deactivate). Alle Endpunkte liegen in der Auth-API.

import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';
import { adminApi } from '../api/adminApi';
import { AdminTabs, Breadcrumbs, ConfirmDialog, EmptyState, ErrorBox, Field, Spinner, useToast, getSortIcon, sortData } from '../components/ui';
import type { Role, TeamDto, UserDto } from '../types';

const MIN_PASSWORD = 12;

export function AdminUsersPage() {
  const { token, user: me } = useAuth();
  const { addToast } = useToast();
  const [users, setUsers] = useState<UserDto[]>([]);
  const [allUsers, setAllUsers] = useState<UserDto[]>([]);
  const [teams, setTeams] = useState<TeamDto[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [actionError, setActionError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirmUser, setConfirmUser] = useState<UserDto | null>(null);
  const [sortConfig, setSortConfig] = useState<{ key: keyof UserDto; direction: 'asc' | 'desc' } | null>(null);
  const [roleFilter, setRoleFilter] = useState<'' | Role>('');
  const [teamFilter, setTeamFilter] = useState<'' | string>('');
  const [searchTerm, setSearchTerm] = useState('');

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
        setAllUsers(u);
        setUsers(u);
        setTeams(t);
        setTeamId((current) => current || t[0]?.id || '');
      })
      .catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  // Client-seitiges Filtern
  const filteredUsers = useMemo(() => {
    let result = users;

    if (roleFilter) {
      result = result.filter((u) => u.role === roleFilter);
    }

    if (teamFilter) {
      result = result.filter((u) => u.teamId === teamFilter);
    }

    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      result = result.filter(
        (u) =>
          u.email.toLowerCase().includes(term) ||
          u.role.toLowerCase().includes(term)
      );
    }

    return result;
  }, [users, roleFilter, teamFilter, searchTerm]);

  // Sortierung
  const sortedUsers = useMemo(() => {
    return sortData(filteredUsers, sortConfig);
  }, [filteredUsers, sortConfig]);

  const teamName = useMemo(() => {
    const map = new Map(teams.map((t) => [t.id, t.name]));
    return (id: string) => map.get(id) ?? id;
  }, [teams]);

  const handleSort = (key: keyof UserDto) => {
    setSortConfig((current) => {
      if (current?.key === key) {
        return { key, direction: current.direction === 'asc' ? 'desc' : 'asc' };
      }
      return { key, direction: 'asc' };
    });
  };

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
      setAllUsers((prev) => [...prev, created]);
      setUsers((prev) => [...prev, created]);
      addToast('Benutzer angelegt', 'success');
      reload();
    } catch (err) {
      setActionError(err);
      addToast('Fehler beim Anlegen des Benutzers', 'error');
    } finally {
      setBusy(false);
    }
  }

  async function onDeactivate(u: UserDto) {
    setConfirmUser(u);
  }

  async function confirmDeactivate() {
    if (!token || !confirmUser) return;
    setActionError(null);
    setNotice(null);
    try {
      await adminApi.deactivateUser(token, confirmUser.id);
      setNotice(`${confirmUser.email} wurde deaktiviert. Ein bereits ausgestelltes Token gilt noch bis zu 15 Minuten.`);
      setAllUsers((prev) => prev.map((u) => (u.id === confirmUser.id ? { ...u, isActive: false } : u)));
      setUsers((prev) => prev.map((u) => (u.id === confirmUser.id ? { ...u, isActive: false } : u)));
      addToast('Benutzer deaktiviert', 'success');
      reload();
    } catch (err) {
      setActionError(err);
      addToast('Fehler beim Deaktivieren', 'error');
    } finally {
      setConfirmUser(null);
    }
  }

  return (
    <div>
      <Breadcrumbs
        items={[
          { label: 'Verwaltung', to: '/admin/users' },
        ]}
      />
      <h1>Verwaltung</h1>
      <AdminTabs />
      {error ? <ErrorBox error={error} /> : null}
      {actionError ? <ErrorBox error={actionError} /> : null}
      {notice ? <div className="noticebox" role="status">{notice}</div> : null}
      {!allUsers.length && !error ? <Spinner label="Benutzer werden geladen…" /> : null}
      {allUsers.length === 0 ? <EmptyState text="Keine Benutzer vorhanden." /> : null}

      <div className="filter-bar">
        <div className="filter-group">
          <Field label="Suche">
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="E-Mail oder Rolle..."
              className="search-field"
            />
          </Field>
        </div>
        <div className="filter-group">
          <Field label="Rolle">
            <select value={roleFilter} onChange={(e) => setRoleFilter(e.target.value as '' | Role)}>
              <option value="">alle</option>
              <option value="Developer">Developer</option>
              <option value="Lead">Lead</option>
              <option value="Admin">Admin</option>
            </select>
          </Field>
        </div>
        <div className="filter-group">
          <Field label="Team">
            <select value={teamFilter} onChange={(e) => setTeamFilter(e.target.value)}>
              <option value="">alle</option>
              {teams.map((t) => (
                <option key={t.id} value={t.id}>{t.name}</option>
              ))}
            </select>
          </Field>
        </div>
      </div>

      {allUsers.length > 0 ? (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th className="sortable" onClick={() => handleSort('email')}>
                  E-Mail {getSortIcon(sortConfig, 'email')}
                </th>
                <th className="sortable" onClick={() => handleSort('role')}>
                  Rolle {getSortIcon(sortConfig, 'role')}
                </th>
                <th className="sortable" onClick={() => handleSort('teamId')}>
                  Team {getSortIcon(sortConfig, 'teamId')}
                </th>
                <th>2FA</th>
                <th className="sortable" onClick={() => handleSort('isActive')}>
                  Status {getSortIcon(sortConfig, 'isActive')}
                </th>
                <th>Aktion</th>
              </tr>
            </thead>
            <tbody>
              {sortedUsers.map((u) => (
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

      <ConfirmDialog
        isOpen={!!confirmUser}
        onClose={() => setConfirmUser(null)}
        onConfirm={confirmDeactivate}
        title="Benutzer deaktivieren"
        message={`Möchtest du ${confirmUser?.email} wirklich deaktivieren? Die Person kann sich danach nicht mehr anmelden.`}
        confirmText="Deaktivieren"
        variant="danger"
      />
    </div>
  );
}
