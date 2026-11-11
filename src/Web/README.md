# SIMS – Weboberfläche (E1–E5)

React + TypeScript + Vite. Alle API-Aufrufe laufen über `src/api/*` und die
zentrale Fehlerbehandlung in `src/api/http.ts`.

## Starten

| Zweck | Befehl |
|---|---|
| Demo mit erfundenen Daten | `npm install` · `npm run dev` (oder `run-dev.bat`) |
| Gegen die echten APIs (Docker läuft) | `run-dev.bat echt` bzw. `VITE_USE_MOCKS=false npm run dev` |
| Prüfen | `npm run build` · `npm test` |
| Im Container | `docker compose up -d --build web` → http://localhost:8080 |

Die Basis-URLs stehen in `public/config.js` (relativ: `/api/auth`, `/api/scan`,
`/api/news`). Im Container leitet nginx weiter, im Dev-Modus der Proxy in
`vite.config.ts`. Damit braucht der Browser kein CORS.

## Wo liegen welche Endpunkte?

| API | Datei | Endpunkte |
|---|---|---|
| Auth | `src/api/authApi.ts` | `/auth/login`, `/auth/mfa/verify`, `/auth/me`, `/auth/logout`, `/auth/mfa/setup`, `/auth/mfa/confirm` |
| Auth (A3) | `src/api/adminApi.ts` | `/users`, `/users/{id}/deactivate`, `/teams` |
| Scan | `src/api/scanApi.ts` | `/projects`, `/projects/{id}/scans`, `/scans/{id}`, `/tickets…`, `/audit` |
| News | `src/api/newsApi.ts` | `/news` |

## Typen aus OpenAPI (E1, Schritt 2–3)

`src/types.ts` ist von Hand aus den Backend-DTOs übernommen. Sobald alle drei
APIs laufen, die Beschreibungen sichern und die Typen erzeugen:

```powershell
mkdir api-specs
curl http://localhost:8081/openapi/v1.json -o api-specs/auth.json
curl http://localhost:8082/openapi/v1.json -o api-specs/scan.json
curl http://localhost:8083/openapi/v1.json -o api-specs/news.json
npx openapi-typescript api-specs/auth.json -o src/api/auth.d.ts
```

Danach `src/types.ts` mit den erzeugten Typen abgleichen.

## Sicherheitsregeln (Prüfliste)

- Token nur im Arbeitsspeicher (`AuthContext`), nie in `localStorage`/`sessionStorage`.
- Kein `innerHTML`, kein `dangerouslySetInnerHTML`; fremde Texte nur als Text.
- Links nur `http`/`https`, mit `rel="noopener noreferrer"`.
- Rollen in der Oberfläche nur für die Anzeige – die Rechte prüft der Server.
