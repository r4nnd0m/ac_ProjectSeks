// E5: Öffentliche Sicherheitsmeldungen über GET {newsUrl}/news, ohne Anmeldung.
// Titel und Quelle werden ausschließlich als Text ausgegeben. Links nur bei
// http/https, in neuem Tab mit rel="noopener noreferrer".

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { newsApi } from '../api/newsApi';
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
    <div className="content">
      <p><Link to="/projects">← Zur Anwendung</Link></p>
      <h1>Öffentliche Meldungen</h1>
      <p className="hint">Sicherheitshinweise aus externen Quellen – ohne Anmeldung lesbar.</p>

      {error ? <ErrorBox error={error} /> : null}
      {!items && !error ? <Spinner label="Meldungen werden geladen…" /> : null}
      {items && items.length === 0 ? <EmptyState text="Zurzeit liegen keine Meldungen vor." /> : null}

      {items && items.length > 0 ? (
        <section>
          {items.map((n, index) => {
            const link = safeHttpUrl(n.link);
            return (
              <article className="newsitem" key={`${n.link}-${index}`}>
                <h2 style={{ margin: '4px 0' }}>{n.title}</h2>
                <p className="meta">
                  Quelle: {n.source} · {formatDate(n.publishedAt)}
                  {link ? (
                    <>
                      {' '}·{' '}
                      <a href={link} target="_blank" rel="noopener noreferrer">
                        Zur Meldung
                      </a>
                    </>
                  ) : null}
                </p>
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
