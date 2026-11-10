## Gemeinsamer Teststand für die Worker-Integration

Auf `main-test` liegen Stefans Worker (D1–D4), die Offline-Demo und die Übergaben.
Die [Teamübersicht](docs/worker/ABSTIMMUNG_TEAM_WORKER.txt) beschreibt den gemeinsamen Testfall.

| Ansprechperson | Einzeldateien |
| --- | --- |
| Christian | [B4 Scanurteil](docs/worker/abstimmung/B4_Scanurteil.txt), [C4 Eskalationsregeln](docs/worker/abstimmung/C4_Eskalationsregeln.txt), [F2 Compose](docs/worker/abstimmung/F2_Compose.txt), [F3 Worker-Image](docs/worker/abstimmung/F3_Workerimage.txt) |
| Patrick | [B1 Datenmodell](docs/worker/abstimmung/B1_Datenmodell.txt), [B2 Upload](docs/worker/abstimmung/B2_SBOM_Upload.txt), [B3 Projekt und Team](docs/worker/abstimmung/B3_Projekt_und_Team.txt), [B5 Scan-Ergebnis](docs/worker/abstimmung/B5_Scanergebnis_lesen.txt) |
| Leo | [C1 Ticketmodell](docs/worker/abstimmung/C1_Ticketmodell.txt), [C2 Ticket-API](docs/worker/abstimmung/C2_Ticket_API.txt), [C3 automatische Tickets](docs/worker/abstimmung/C3_Automatische_Tickets.txt), [C5 Audit](docs/worker/abstimmung/C5_Audit.txt) |

Für ein neues Arbeitspaket im vorhandenen Klon zuerst `git fetch origin` ausführen.
Zum Beispiel startet `git switch --no-track -c patrick/b1-datenmodell origin/main-test`
einen eigenen Branch für B1. Beim Pull Request für den gemeinsamen Test `main-test` als Ziel auswählen.

Die [Offline-Demo](docs/worker/DEMO_Anleitung.txt) zeigt die Abläufe mit Testdaten.
Der Worker verwendet die gemeinsamen SQL-Modelle. Für den vollständigen Betrieb fehlen
noch die verbindlichen Regeln für Scanurteile (B4) und Eskalationen (C4).

### Scan-Datenbank einrichten

1. In PowerShell im Projektordner den lokalen Microsoft SQL Server mit `docker compose up -d sql` starten.
2. Die Umgebungsvariable `ConnectionStrings__Sims` auf die Ziel-Datenbank setzen.
   Scan-API und Worker müssen dieselbe Datenbank verwenden.
3. `.\scripts\scan-db.ps1` ausführen. EF Core legt zuerst die vier Scan-Tabellen aus B1
   an und ergänzt danach Tickets und deren Änderungshistorie.

Für das spätere Cloud-Deployment erzeugt `.\scripts\scan-db.ps1 -OutputSql .\scan-migration.sql`
ein prüfbares SQL-Skript. Bei der Ausführung auf der Ziel-Datenbank überspringt es
bereits eingespielte Migrationen. Beide Befehle benötigen das .NET 10 SDK und
`dotnet ef` 10. [Details und Testaufruf](docs/worker/D3_Scanablauf_Uebergabe.txt)

 Grundlegende Werkzeuge installieren
 Bevor du das Projekt herunterlädst, müssen ein paar Programme auf deinem PC installiert sein.

 .NET 10 SDK herunterladen und installieren (wichtig für die Programmierung).
 Docker Desktop installieren (wichtig für die Datenbanken und Dienste).
 Git installieren (um den Code herunterzuladen).
 Visual Studio Rider (oder Visual Studio / VS Code) als Code-Editor installieren.   
 Überprüfung: Öffne dein Terminal (Eingabeaufforderung / PowerShell) und tippe dotnet --version (muss Version 10.x anzeigen) und docker --version.  

Git abgleichen und ziehen
Git diff
Git pull


Creds in eine .env schieben
cp .env.example .env

Docker container erstellen btw starten

 docker compose up -d sql (oder psql wenn postgres erwüscht)

 docker compose up -d redis

 !!!WICHTIG!!!
Nicht blind reinkopieren. IMMER Pfade kontrollieren btw anpassen

### Patrick – Scan-API A + Security-Basis

- B1: Datenmodell / Entity-Struktur
- B2: SBOM-Upload und Parsing
- B3: Projekt- und Teamkontext
- B5: Scan-Ergebnis lesen / Details
- S1: Security-Basis (Auth, CORS, Rate Limiting, Header-Security)






