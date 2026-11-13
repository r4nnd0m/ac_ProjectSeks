// E4: Teams anzeigen (GET /teams) und anlegen (POST /teams, 409 bei doppeltem Namen). Nur Admins, Auth-API.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';
import { adminApi } from '../api/adminApi';
import { AdminTabs } from '../components/AdminTabs';
import { EmptyState, ErrorBox, Field, Spinner } from '../components/ui';
import type { TeamDto } from '../types';

export function AdminTeamsPage() {
  const { token } = useAuth();
  const [teams, setTeams] = useState<TeamDto[] | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [createError, setCreateError] = useState<unknown>(null);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    adminApi.listTeams(token).then(setTeams).catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!token) return;
    setBusy(true);
    setCreateError(null);
    try {
      await adminApi.createTeam(token, name.trim());
      setName('');
      reload();
    } catch (err) {
      setCreateError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>Verwaltung</h1>
      <AdminTabs />
      {error ? <ErrorBox error={error} /> : null}
      {!teams && !error ? <Spinner label="Teams werden geladen…" /> : null}
      {teams && teams.length === 0 ? <EmptyState text="Noch keine Teams." /> : null}
      {teams && teams.length > 0 ? (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Kennung</th>
              </tr>
            </thead>
            <tbody>
              {teams.map((t) => (
                <tr key={t.id}>
                  <td>{t.name}</td>
                  <td><code>{t.id}</code></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      <div className="card">
        <h2>Neues Team anlegen</h2>
        <form onSubmit={onCreate}>
          <Field label="Teamname">
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
          </Field>
          {createError ? <ErrorBox error={createError} /> : null}
          <div style={{ marginTop: 'var(--spacing-md)' }}>
            <button type="submit" disabled={busy || !name.trim()}>
              {busy ? 'Wird angelegt…' : 'Team anlegen'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
