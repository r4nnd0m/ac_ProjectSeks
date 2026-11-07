// E5: Öffentliche Sicherheitsmeldungen über GET /news. Fremde Inhalte werden
// ausschließlich als Text ausgegeben; Links nur bei http/https. Für jeden
// Zustand (Laden, leer, keine Rechte, abgelaufene Anmeldung, unerreichbarer
// Server) gibt es eine verständliche Meldung.

import { useCallback, useEffect, useState } from 'react';
import { newsApi } from '../api/newsApi';
import { ApiError } from '../api/http';
import { EmptyState, ErrorBox, Spinner } from '../components/ui';
import { formatDate, safeHttpUrl } from '../utils';
import type { NewsItemDto } from '../types';

export function NewsPage() {
  const [items, setItems] = useState<NewsItemDto[] | null>(null);
  const [error, setError] = useState<unknown>(null);

  const reload = useCallback(() => {
    setError(null);
    setItems(null);
    newsApi.list().then(setItems).catch(setError);
  }, []);

  useEffect(reload, [reload]);

  return (
    <div>
      <h1>Öffentliche Meldungen</h1>
      <p className="hint">
        Sicherheitshinweise aus externen Quellen – immer ohne Anmeldung lesbar, immer als reiner Text.
      </p>

      {error instanceof ApiError && error.kind === 'expired' ? (
        <div className="errorbox" role="alert">
          Deine Anmeldung ist abgelaufen. Die öffentlichen Meldungen kannst du aber weiterhin lesen –
          bitte melde dich oben neu an.
        </div>
      ) : null}
      {error instanceof ApiError && error.kind === 'forbidden' ? (
        <div className="errorbox" role="alert">
          Diese Meldungen sind für dich nicht freigeschaltet. Falls das ein Fehler ist, frage eine
          Admin-Person.
        </div>
      ) : null}
      {error && !(error instanceof ApiError && (error.kind === 'expired' || error.kind === 'forbidden')) ? (
        <ErrorBox error={error}>
          {error instanceof ApiError && error.kind === 'unreachable' ? (
            <span>Tipp für die Entwicklung: Im Demo-Modus (VITE_USE_MOCKS=true) antwortet eine erfundene News-Quelle.</span>
          ) : null}
        </ErrorBox>
      ) : null}

      {!items && !error ? <Spinner label="Meldungen werden geladen…" /> : null}
      {items && items.length === 0 ? (
        <EmptyState text="Zurzeit liegen keine Meldungen vor." />
      ) : null}

      {items && items.length > 0 ? (
        <section>
          {items.map((n) => {
            const link = safeHttpUrl(n.url);
            return (
              <article className="newsitem" key={n.id}>
                <h2 style={{ margin: '4px 0' }}>{n.title}</h2>
                <p className="meta">
                  Quelle: {n.source} · Veröffentlicht am {formatDate(n.publishedAt)}
                  {link ? (
                    <>
                      {' '}·{' '}
                      <a href={link} target="_blank" rel="noopener noreferrer">
                        Weiter zur Meldung (öffnet in neuem Fenster)
                      </a>
                    </>
                  ) : n.url ? (
                    <> · <span className="hint">Link unterdrückt (kein http/https)</span></>
                  ) : null}
                </p>
                {n.summary ? <p>{n.summary}</p> : null}
              </article>
            );
          })}
        </section>
      ) : null}

      <p>
        <button type="button" className="secondary" onClick={reload}>Neu laden</button>
      </p>
    </div>
  );
}
