// E3: Projekte auflisten und anlegen. Developer und Lead sehen das eigene
// Team, Admins alle (die echte Rechteprüfung ist Aufgabe des Backends – hier nur Darstellung).

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { EmptyState, ErrorBox, Field, Spinner } from '../components/ui';
import { formatDate } from '../utils';
import type { ProjectDto } from '../types';

export function ProjectsPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const [projects, setProjects] = useState<ProjectDto[] | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [createError, setCreateError] = useState<unknown>(null);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    scanApi
      .listProjects(token)
      .then(setProjects)
      .catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!token) return;
    setBusy(true);
    setCreateError(null);
    try {
      const created = await scanApi.createProject(token, name.trim());
      navigate(`/projects/${created.id}`);
    } catch (err) {
      setCreateError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>Projekte</h1>
      {error ? <ErrorBox error={error} /> : null}
      {!projects && !error ? <Spinner label="Projekte werden geladen…" /> : null}
      {projects && projects.length === 0 ? (
        <EmptyState text="Noch keine Projekte in deinem Sichtbereich. Lege rechts ein erstes an." />
      ) : null}
      {projects && projects.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Team</th>
              <th>Angelegt am</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((p) => (
              <tr key={p.id}>
                <td>
                  <Link to={`/projects/${p.id}`}>{p.name}</Link>
                </td>
                <td>{p.teamName}</td>
                <td>{formatDate(p.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      <div className="card">
        <h2>Neues Projekt anlegen</h2>
        <form onSubmit={onCreate}>
          <Field label="Projektname" hint="Das Projekt gehört automatisch zu deinem Team.">
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
          </Field>
          {createError ? <ErrorBox error={createError} /> : null}
          <button type="submit" disabled={busy || !name.trim()}>
            {busy ? 'Wird angelegt…' : 'Projekt anlegen'}
          </button>
        </form>
      </div>
    </div>
  );
}
