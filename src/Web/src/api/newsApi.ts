// API-Client für den News-Service: GET /news, öffentlich ohne Anmeldung (E5).

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
