// API-Client für die News-API (Port 8083).
//
// Aus dem Auftrag bekannt: GET /news (öffentlich). Weitere Endpunkte sind
// nicht vereinbart; PROVISORISCH, siehe Seite „Hinweise“.

import { simsConfig, USE_MOCKS } from '../config';
import type { NewsItemDto } from '../types';
import { request } from './http';
import { mockListNews } from './mock/server';

const base = () => simsConfig.newsUrl;

export const newsApi = {
  list(): Promise<NewsItemDto[]> {
    if (USE_MOCKS) return mockListNews();
    return request<NewsItemDto[]>(base(), '/news');
  },
};
