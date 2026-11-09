// SIMS – Laufzeitkonfiguration für die Weboberfläche.
// Diese Datei wird vor dem eigentlichen Bundle geladen und liefert die
// Basis-URLs der drei APIs über window.SIMS_CONFIG (Auftrag E1).
//
// Die Werte sind bewusst NICHT im Bundle enthalten, damit der Betrieb sie
// ohne Neubaustand anpassen kann.
window.SIMS_CONFIG = {
  // Auth-API (Anmeldung, Zwei-Faktor) – lokal Port 8081
  authUrl: 'http://localhost:8081',
  // Scan-API (Projekte, Scans, Tickets, Verwaltung) – lokal Port 8082
  scanUrl: 'http://localhost:8082',
  // News-API (öffentliche Sicherheitsmeldungen) – lokal Port 8083
  newsUrl: 'http://localhost:8083',
};
