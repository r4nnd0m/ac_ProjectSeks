// E3: Projektseite – letzte Scans und SBOM-Upload (CycloneDX-JSON,
// Prüfung auf JSON und maximal 5 MB, dann POST /projects/{id}/scans?version=…).

import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { scanApi } from '../api/scanApi';
import { describeError } from '../api/http';
import { EmptyState, ErrorBox, Field, Spinner, StatusBadge } from '../components/ui';
import { verdictLabels } from '../utils';
import { formatDateTime, formatDate } from '../utils';
import type { ProjectDto, ScanSummaryDto } from '../types';

const MAX_SBOM_BYTES = 5 * 1024 * 1024; // 5 MB (Auftrag E3)

/** Vor dem Versand: gültiges JSON, CycloneDX mit components-Feld, Größe. */
export function validateSbom(text: string): string | null {
  if (new TextEncoder().encode(text).byteLength > MAX_SBOM_BYTES) {
    return 'Die Datei ist größer als 5 MB. Bitte vorher verkleinern.';
  }
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return 'Die Datei enthält kein gültiges JSON.';
  }
  if (typeof parsed !== 'object' || parsed === null) return 'Erwartet wird ein JSON-Objekt.';
  const bom = parsed as { bomFormat?: unknown; components?: unknown };
  if (bom.bomFormat !== 'CycloneDX') return 'bomFormat muss "CycloneDX" sein.';
  if (!Array.isArray(bom.components)) return 'Es wird ein components-Feld mit einer Liste erwartet.';
  return null;
}

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { token } = useAuth();
  const navigate = useNavigate();
  const fileRef = useRef<HTMLInputElement>(null);

  const [project, setProject] = useState<ProjectDto | null>(null);
  const [scans, setScans] = useState<ScanSummaryDto[] | null>(null);
  const [error, setError] = useState<unknown>(null);

  const [version, setVersion] = useState('');
  const [sbomText, setSbomText] = useState('');
  const [sbomFileName, setSbomFileName] = useState('');
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);

  const reload = useCallback(() => {
    if (!token || !id) return;
    setError(null);
    Promise.all([scanApi.getProject(token, id), scanApi.listProjectScans(token, id)])
      .then(([p, s]) => {
        setProject(p);
        setScans(s);
      })
      .catch(setError);
  }, [token, id]);

  useEffect(() => {
    reload();
    const timer = setInterval(reload, 5_000); // laufende Scans sichtbar halten
    return () => clearInterval(timer);
  }, [reload]);

  function onFilePick(file: File | undefined) {
    if (!file) return;
    setSbomFileName(file.name);
    setUploadError(null);
    const reader = new FileReader();
    reader.onerror = () => setUploadError('Die Datei konnte nicht gelesen werden.');
    reader.onload = () => setSbomText(String(reader.result ?? ''));
    reader.readAsText(file);
  }

  async function onUpload(e: FormEvent) {
    e.preventDefault();
    if (!token || !id) return;
    setUploadError(null);
    if (!version.trim()) {
      setUploadError('Bitte eine Versionsangabe eintragen.');
      return;
    }
    const problem = validateSbom(sbomText);
    if (problem) {
      setUploadError(problem);
      return;
    }
    setUploading(true);
    try {
      const created = await scanApi.createScan(token, id, version.trim(), sbomText);
      navigate(`/scans/${created.scanId}`);
    } catch (err) {
      setUploadError(describeError(err));
    } finally {
      setUploading(false);
    }
  }

  return (
    <div>
      <h1>{project ? `Projekt: ${project.name}` : 'Projekt'}</h1>
      {project ? (
        <p className="hint">
          Angelegt am {formatDate(project.createdAt)} · Projektkennung{' '}
          <code>{project.id}</code>
        </p>
      ) : null}
      {error ? <ErrorBox error={error} /> : null}
      {!project && !error ? <Spinner label="Projekt wird geladen…" /> : null}

      <h2>Letzte Scans</h2>
      {!scans ? <Spinner label="Scans werden geladen…" /> : null}
      {scans && scans.length === 0 ? <EmptyState text="Für dieses Projekt wurde noch nichts gescannt." /> : null}
      {scans && scans.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>Version</th>
              <th>Status</th>
              <th>Urteil</th>
              <th>Erstellt</th>
            </tr>
          </thead>
          <tbody>
            {scans.map((s) => (
              <tr key={s.id}>
                <td>
                  <Link to={`/scans/${s.id}`}>{s.version}</Link>
                </td>
                <td><StatusBadge status={s.status} /></td>
                <td>{s.verdict ? verdictLabels[s.verdict] : <span className="hint">–</span>}</td>
                <td>{formatDateTime(s.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      <div className="card">
        <h2>SBOM hochladen und Scan starten</h2>
        <p className="hint">
          CycloneDX-JSON (Format 1.5), maximal 5 MB. Das JSON wird unverändert an{' '}
          <code>POST /projects/{'{id}'}/scans?version=…</code> gesendet.
          {' '}
          <button type="button" className="linkbtn" onClick={() => { setSbomText(EXAMPLE_SBOM); setSbomFileName('beispiel-sbom.json'); setUploadError(null); }}>
            Beispiel-SBOM einfügen
          </button>
        </p>
        <form onSubmit={onUpload}>
          <Field label="Versionsangabe" hint="Zum Beispiel 1.4.0 – erscheint später im Scan.">
            <input value={version} onChange={(e) => setVersion(e.target.value)} placeholder="1.0.0" required maxLength={50} />
          </Field>
          <Field label="CycloneDX-Datei" hint="Alternativ kannst du den JSON-Inhalt unten direkt einfügen.">
            <input
              ref={fileRef}
              type="file"
              accept=".json,application/json"
              onChange={(e) => onFilePick(e.target.files?.[0])}
            />
          </Field>
          <Field label="JSON-Inhalt">
            <textarea rows={8} value={sbomText} onChange={(e) => { setSbomText(e.target.value); setSbomFileName(''); }} />
          </Field>
          {sbomFileName ? <p className="hint">Geladene Datei: {sbomFileName}</p> : null}
          {uploadError ? <div className="errorbox" role="alert">{uploadError}</div> : null}
          <button type="submit" disabled={uploading || !sbomText.trim()}>
            {uploading ? 'Upload läuft…' : 'Scan starten'}
          </button>{' '}
          <button type="button" className="secondary" onClick={reload}>Aktualisieren</button>
        </form>
      </div>
    </div>
  );
}

/** Kleines erfundenes Beispiel für die Vorführung. */
export const EXAMPLE_SBOM = JSON.stringify(
  {
    bomFormat: 'CycloneDX',
    specVersion: '1.5',
    components: [
      {
        type: 'library',
        name: 'Newtonsoft.Json',
        version: '12.0.1',
        purl: 'pkg:nuget/Newtonsoft.Json@12.0.1',
      },
      {
        type: 'library',
        name: 'Beispiel.Kasse.dll',
        version: '1.2.3',
        purl: 'pkg:nuget/Beispiel.Kasse@1.2.3',
      },
    ],
  },
  null,
  2,
);
