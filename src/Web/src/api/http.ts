// Kleiner fetch-Helfer für alle drei APIs mit zentraler Fehlerbehandlung (E5):
//   401 -> zurück zur Anmeldung, 403 -> „Keine Berechtigung“,
//   429 -> „Zu viele Anfragen, bitte kurz warten“, Netzfehler -> „Server nicht erreichbar“,
//   alles andere -> allgemeine Meldung ohne technische Details.

export type ApiErrorKind =
  | 'unreachable'
  | 'expired'
  | 'forbidden'
  | 'notfound'
  | 'conflict'
  | 'toomany'
  | 'input'
  | 'server';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly kind: ApiErrorKind = 'server',
    /** Einzelmeldungen aus ProblemDetails (z. B. Passwortregeln). */
    public readonly details: string[] = [],
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  token?: string | null;
  json?: unknown;
  /** Roh-Text-Body (CycloneDX-JSON beim Scan-Upload). */
  textBody?: string;
  contentType?: string;
  query?: Record<string, string | number | boolean | undefined>;
  signal?: AbortSignal;
}

// ---------------------------------------------------------------- 401 ----

let unauthorizedHandler: (() => void) | null = null;

/** Die Anmeldung registriert hier, was bei 401 passieren soll (Sitzung verwerfen). */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

// ---------------------------------------------------------------- URL ----

export function buildUrl(baseUrl: string, path: string, query?: RequestOptions['query']): string {
  const origin = typeof window !== 'undefined' && window.location ? window.location.origin : 'http://sims.invalid';
  const url = new URL(baseUrl.replace(/\/+$/, '') + path, origin);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined) url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

// ----------------------------------------------------------- Meldungen ----

export const MESSAGES = {
  unreachable: 'Server nicht erreichbar. Bitte später erneut versuchen.',
  expired: 'Deine Anmeldung ist abgelaufen. Bitte melde dich erneut an.',
  forbidden: 'Keine Berechtigung.',
  notfound: 'Nicht gefunden.',
  toomany: 'Zu viele Anfragen, bitte kurz warten.',
  server: 'Es ist ein Fehler aufgetreten. Bitte später erneut versuchen.',
  input: 'Die Eingaben wurden nicht angenommen.',
  conflict: 'Die Aktion ist im aktuellen Zustand nicht möglich.',
} as const;

export function describeError(err: unknown): string {
  if (err instanceof ApiError) {
    return err.details.length > 0 ? `${err.message} ${err.details.join(' ')}` : err.message;
  }
  if (err instanceof Error && err.name === 'AbortError') return 'Die Anfrage wurde abgebrochen.';
  return MESSAGES.server;
}

/** Liest Titel und Einzelfehler aus ProblemDetails / ValidationProblemDetails. */
async function readProblem(response: Response): Promise<{ title: string | null; details: string[] }> {
  try {
    const text = await response.text();
    if (!text) return { title: null, details: [] };
    const body = JSON.parse(text) as { title?: unknown; errors?: unknown };
    const title = typeof body.title === 'string' ? body.title : null;
    const details: string[] = [];
    if (Array.isArray(body.errors)) {
      for (const e of body.errors) if (typeof e === 'string') details.push(e);
    } else if (body.errors && typeof body.errors === 'object') {
      for (const list of Object.values(body.errors as Record<string, unknown>)) {
        if (Array.isArray(list)) for (const e of list) if (typeof e === 'string') details.push(e);
      }
    }
    return { title, details };
  } catch {
    return { title: null, details: [] };
  }
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
  } catch (err) {
    if (err instanceof Error && err.name === 'AbortError') throw err;
    throw new ApiError(0, MESSAGES.unreachable, 'unreachable');
  }

  if (response.status === 401) {
    // Nur bei Anfragen mit Token zur Anmeldung zurück; ein falsches Passwort ist kein Sitzungsende.
    if (token && unauthorizedHandler) unauthorizedHandler();
    throw new ApiError(401, token ? MESSAGES.expired : 'Anmeldung fehlgeschlagen.', 'expired');
  }
  if (response.status === 403) throw new ApiError(403, MESSAGES.forbidden, 'forbidden');
  if (response.status === 404) throw new ApiError(404, MESSAGES.notfound, 'notfound');
  if (response.status === 429) throw new ApiError(429, MESSAGES.toomany, 'toomany');
  if (response.status === 400 || response.status === 409) {
    const problem = await readProblem(response);
    const kind = response.status === 409 ? 'conflict' : 'input';
    // Titel und Einzelfehler stammen aus der eigenen API und sind fachliche Texte.
    throw new ApiError(response.status, problem.title ?? MESSAGES[kind], kind, problem.details);
  }
  if (!response.ok) {
    // Keine technischen Details anzeigen (E5).
    throw new ApiError(response.status, MESSAGES.server, 'server');
  }

  if (response.status === 204) return undefined as T;
  const type = response.headers.get('content-type') ?? '';
  if (type.includes('json')) return (await response.json()) as T;
  return (await response.text()) as unknown as T;
}
