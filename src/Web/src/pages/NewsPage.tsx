// E5: Öffentliche Sicherheitsmeldungen über GET {newsUrl}/news, ohne Anmeldung.
// Titel und Quelle werden ausschließlich als Text ausgegeben. Links nur bei
// http/https, in neuem Tab mit rel="noopener noreferrer".

import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { newsApi } from '../api/newsApi';
import { EmptyState, ErrorBox, Skeleton, Spinner, useToast } from '../components/ui';
import { formatDate, safeHttpUrl } from '../utils';
import type { NewsItemDto } from '../types';

export function NewsPage() {
  const { addToast } = useToast();
  const navigate = useNavigate();
  const [items, setItems] = useState<NewsItemDto[] | null>(null);
  const [error, setError] = useState<unknown>(null);

  const reload = useCallback(() => {
    setError(null);
    setItems(null);
    newsApi.list().then(setItems).catch(setError).finally(() => {
      addToast('Meldungen aktualisiert', 'info');
    });
  }, []);

  useEffect(reload, [reload]);

  return (
    <div>
      <main className="content">
        <div className="public-content">
          <h1>Öffentliche Meldungen</h1>
          <p className="hint">Sicherheitshinweise aus externen Quellen – ohne Anmeldung lesbar.</p>

          <div className="public-actions">
            <button type="button" className="secondary" onClick={() => navigate('/projects')}>
              ← Zur Anwendung
            </button>
            <button type="button" className="secondary" onClick={reload}>
              Neu laden
            </button>
          </div>

          {error ? <ErrorBox error={error} /> : null}
          {!items && !error ? <Spinner label="Meldungen werden geladen…" /> : null}
          {items && items.length === 0 ? <EmptyState text="Zurzeit liegen keine Meldungen vor." /> : null}

          {items && items.length > 0 ? (
            <div className="card news-list">
              {items.map((n, index) => {
                const link = safeHttpUrl(n.link);
                return (
                  <article className="news-card" key={`${n.link}-${index}`}>
                    <h2>{n.title}</h2>
                    <div className="news-meta">
                      <span className="badge source-badge">{n.source}</span>
                      <span className="hint">{formatDate(n.publishedAt)}</span>
                      {link ? (
                        <>
                          {' '}·{' '}
                          <a href={link} target="_blank" rel="noopener noreferrer">
                            Zur Meldung <span aria-hidden="true">↗</span>
                          </a>
                        </>
                      ) : null}
                    </div>
                  </article>
                );
              })}
            </div>
          ) : null}
        </div>
      </main>
    </div>
  );
}
