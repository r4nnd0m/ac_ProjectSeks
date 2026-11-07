// Liest die Basis-URLs aus public/config.js (window.SIMS_CONFIG, Auftrag E1).

export interface SimsRunConfig {
  authUrl: string;
  scanUrl: string;
  newsUrl: string;
}

declare global {
  interface Window {
    SIMS_CONFIG?: Partial<SimsRunConfig>;
  }
}

const fallback: SimsRunConfig = {
  authUrl: 'http://localhost:8081',
  scanUrl: 'http://localhost:8082',
  newsUrl: 'http://localhost:8083',
};

export const simsConfig: SimsRunConfig = {
  authUrl: (typeof window !== 'undefined' ? window.SIMS_CONFIG?.authUrl : undefined) ?? fallback.authUrl,
  scanUrl: (typeof window !== 'undefined' ? window.SIMS_CONFIG?.scanUrl : undefined) ?? fallback.scanUrl,
  newsUrl: (typeof window !== 'undefined' ? window.SIMS_CONFIG?.newsUrl : undefined) ?? fallback.newsUrl,
};

/**
 * Demo-Modus: erfundene Testdaten statt echter API-Aufrufe.
 * Aktiviert durch VITE_USE_MOCKS=true. Ohne gesetzte Variable läuft die
 * Demo automatisch, damit die Oberfläche ohne Backends vorführbar ist.
 * Für echte API-Verbindungen VITE_USE_MOCKS=false setzen.
 */
export const USE_MOCKS = (import.meta.env.VITE_USE_MOCKS ?? 'true') !== 'false';
