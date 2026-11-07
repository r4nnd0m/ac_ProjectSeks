// Kleiner fetch-Helfer für alle drei APIs.
// Fehler werden in verständliche deutsche Meldungen übersetzt (Auftrag E5):
// unerreichbarer Server, abgelaufene Anmeldung, fehlende Rechte.

/** Fehler aus API-Sicht mit einer vorformulierten deutschen Meldung. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    /** Maschinelesbarer Merker für die Oberfläche. */
    public readonly kind: 'unreachable' | 'expired' | 'forbidden' | 'notfound' | 'server' | 'input' = 'server',
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  token?: string | null;
  /** Als JSON gesendet. */
  json?: unknown;
  /** Roh-Text-Body (z. B. CycloneDX-JSON beim Scan-Upload). */
  textBody?: string;
  contentType?: string;
  query?: Record<string, string | number | boolean | undefined>;
  signal?: AbortSignal;
}

export function buildUrl(baseUrl: string, path: string, query?: RequestOptions['query']): string {
  const url = new URL(baseUrl.replace(/\/+$/, '') + path);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined) url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

/** Melde-Texte für die in E5 geforderten Zustände. */
export function describeError(err: unknown): string {
  if (err instanceof ApiError) return err.message;
  if (err instanceof Error && err.name === 'AbortError') return 'Die Anfrage wurde abgebrochen.';
  return 'Unerwarteter Fehler in der Oberfläche. Bitte erneut versuchen.';
}

export async function request<T>(baseUrl: string, path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', token, json, textBody, contentType, query, signal } = options;

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  let body: string | undefined;
  if (json !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(json);
  } else if (textBody !== undefined) {
    headers['Content-Type'] = contentType ?? 'application/json';
    body = textBody;
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(baseUrl, path, query), { method, headers, body, signal });
  } catch {
    throw new ApiError(
      0,
      'Der Server ist nicht erreichbar. Läuft der Dienst? (Basis-URL in public/config.js prüfen)',
      'unreachable',
    );
  }

  if (response.status === 401) {
    throw new ApiError(401, 'Deine Anmeldung ist abgelaufen. Bitte melde dich erneut an.', 'expired');
  }
  if (response.status === 403) {
    throw new ApiError(403, 'Dafür fehlen dir die Rechte. Maßgeblich ist die Prüfung im Backend.', 'forbidden');
  }
  if (response.status === 404) {
    throw new ApiError(404, 'Der angeforderte Datensatz wurde nicht gefunden.', 'notfound');
  }
  if (!response.ok) {
    const text = await response.text().catch(() => '');
    throw new ApiError(response.status, `Der Server antwortete mit ${response.status}. ${text}`.trim(), 'server');
  }

  if (response.status === 204) return undefined as T;
  const contentTypeOut = response.headers.get('content-type') ?? '';
  if (contentTypeOut.includes('json')) return (await response.json()) as T;
  return (await response.text()) as unknown as T;
}
