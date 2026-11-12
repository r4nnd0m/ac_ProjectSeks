// E3: Projekte auflisten und anlegen (GET/POST /projects). Developer und Lead
// sehen das eigene Team, Admins alle – das filtert die Scan-API.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { Breadcrumbs, EmptyState, ErrorBox, Field, Spinner, useToast, getSortIcon, sortData } from '../components/ui';
import { formatDate } from '../utils';
import type { ProjectDto } from '../types';

export function ProjectsPage() {
  const { token } = useAuth();
  const { addToast } = useToast();
  const location = useLocation();

  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [allProjects, setAllProjects] = useState<ProjectDto[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [createError, setCreateError] = useState<unknown>(null);
  const [sortConfig, setSortConfig] = useState<{ key: keyof ProjectDto; direction: 'asc' | 'desc' } | null>(null);

  const reload = useCallback(() => {
    if (!token) return;
    setError(null);
    scanApi
      .listProjects(token)
      .then((data) => {
        setAllProjects(data);
        setProjects(data);
      })
      .catch(setError);
  }, [token]);

  useEffect(reload, [reload]);

  // Client-seitiges Filtern und Sortieren
  const filteredProjects = sortData(projects, sortConfig);

  const handleSort = (key: keyof ProjectDto) => {
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
    setCreateError(null);
    try {
      const created = await scanApi.createProject(token, name.trim());
      setAllProjects((prev) => [...prev, created]);
      setProjects((prev) => [...prev, created]);
      addToast('Projekt angelegt', 'success');
      navigate(`/projects/${created.id}`);
    } catch (err) {
      setCreateError(err);
      addToast('Fehler beim Anlegen des Projekts', 'error');
    } finally {
      setBusy(false);
    }
  }

  const navigate = useNavigate();

  return (
    <div>
      <Breadcrumbs
        items={[
          { label: 'Projekte', to: '/projects' },
        ]}
      />
      <h1>Projekte</h1>
      {error ? <ErrorBox error={error} /> : null}
      {!allProjects.length && !error ? <Spinner label="Projekte werden geladen…" /> : null}
      {allProjects.length === 0 ? (
        <EmptyState text="Noch keine Projekte in deinem Sichtbereich. Lege rechts ein erstes an." />
      ) : null}
      {allProjects.length > 0 ? (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th className="sortable" onClick={() => handleSort('name')}>
                  Name {getSortIcon(sortConfig, 'name')}
                </th>
                <th className="sortable" onClick={() => handleSort('createdAt')}>
                  Angelegt am {getSortIcon(sortConfig, 'createdAt')}
                </th>
              </tr>
            </thead>
            <tbody>
              {filteredProjects.map((p) => (
                <tr key={p.id}>
                  <td>
                    <Link to={`/projects/${p.id}`}>{p.name}</Link>
                  </td>
                  <td>{formatDate(p.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      <div className="card">
        <h2>Neues Projekt anlegen</h2>
        <form onSubmit={onCreate}>
          <Field label="Projektname" hint="Das Projekt gehört automatisch zu deinem Team.">
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
          </Field>
          {createError ? <ErrorBox error={createError} /> : null}
          <div style={{ marginTop: 'var(--spacing-md)' }}>
            <button type="submit" disabled={busy || !name.trim()}>
              {busy ? 'Wird angelegt…' : 'Projekt anlegen'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
