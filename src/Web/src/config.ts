// Liest die Basis-URLs aus public/config.js (window.SIMS_CONFIG, Auftrag E1).
// Im Code stehen keine Hostnamen. Ohne config.js gilt derselbe Ursprung wie
// die Weboberfläche (nginx bzw. der Vite-Proxy leiten /api/... weiter).

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
  authUrl: '/api/auth',
  scanUrl: '/api/scan',
  newsUrl: '/api/news',
};

const runtime = typeof window !== 'undefined' ? window.SIMS_CONFIG : undefined;

export const simsConfig: SimsRunConfig = {
  authUrl: runtime?.authUrl ?? fallback.authUrl,
  scanUrl: runtime?.scanUrl ?? fallback.scanUrl,
  newsUrl: runtime?.newsUrl ?? fallback.newsUrl,
};

/**
 * Demo-Modus: erfundene Testdaten statt echter API-Aufrufe.
 * VITE_USE_MOCKS=true schaltet ihn ein. Ohne gesetzte Variable läuft die
 * Demo, damit die Oberfläche ohne Backends vorführbar ist. Das Docker-Image
 * baut mit VITE_USE_MOCKS=false.
 */
export const USE_MOCKS = (import.meta.env.VITE_USE_MOCKS ?? 'true') !== 'false';
