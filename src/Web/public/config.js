// SIMS – Laufzeitkonfiguration der Weboberfläche (Auftrag E1).
// Wird vor dem Bundle geladen und liefert die Basis-URLs der drei APIs.
//
// Die Oberfläche spricht alle APIs über denselben Ursprung an:
//   - im Docker-Betrieb leitet nginx /api/auth, /api/scan und /api/news weiter,
//   - im Entwicklungsmodus (npm run dev) erledigt das der Proxy in vite.config.ts.
// So braucht der Browser kein CORS. Für einen anderen Betrieb kann diese Datei
// ersetzt werden, ohne das Bundle neu zu bauen.
window.SIMS_CONFIG = {
  authUrl: '/api/auth',
  scanUrl: '/api/scan',
  newsUrl: '/api/news',
};
