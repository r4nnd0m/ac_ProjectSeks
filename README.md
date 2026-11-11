## Gemeinsamer Teststand für die Worker-Integration

Auf `infra-test` sind der Worker (D1–D4), die Regeln aus B4/C4 und der Docker-Aufbau des Teams verbunden.
Die [Teamübersicht](docs/worker/ABSTIMMUNG_TEAM_WORKER.txt) beschreibt den gemeinsamen Testfall.

| Ansprechperson | Einzeldateien |
| --- | --- |
| Christian | [B4 Scanurteil](docs/worker/abstimmung/B4_Scanurteil.txt), [C4 Eskalationsregeln](docs/worker/abstimmung/C4_Eskalationsregeln.txt), [F2 Compose](docs/worker/abstimmung/F2_Compose.txt), [F3 Worker-Image](docs/worker/abstimmung/F3_Workerimage.txt) |
| Patrick | [B1 Datenmodell](docs/worker/abstimmung/B1_Datenmodell.txt), [B2 Upload](docs/worker/abstimmung/B2_SBOM_Upload.txt), [B3 Projekt und Team](docs/worker/abstimmung/B3_Projekt_und_Team.txt), [B5 Scan-Ergebnis](docs/worker/abstimmung/B5_Scanergebnis_lesen.txt) |
| Leo | [C1 Ticketmodell](docs/worker/abstimmung/C1_Ticketmodell.txt), [C2 Ticket-API](docs/worker/abstimmung/C2_Ticket_API.txt), [C3 automatische Tickets](docs/worker/abstimmung/C3_Automatische_Tickets.txt), [C5 Audit](docs/worker/abstimmung/C5_Audit.txt) |

Für ein neues Arbeitspaket im vorhandenen Klon zuerst `git fetch origin` ausführen.
Zum Beispiel startet `git switch --no-track -c patrick/b1-datenmodell origin/main-test`
einen eigenen Branch für B1. Beim Pull Request für den gemeinsamen Test `main-test` als Ziel auswählen.

Die frühere Offline-Demo unter `examples/Sims.Worker.Demo` ist deaktiviert.
Der Hintergrunddienst `ScanPollingService` holt Aufträge aus SQL. Der Dienstname in
Docker bleibt `worker`. Die normalen Dienste verwenden die folgenden Regeln.

### Scanurteil und Ticketfristen

| Aus den Funden eines Scans | Urteil |
| --- | --- |
| Mindestens ein KEV-Eintrag, ein kritischer Fund oder EPSS ab einschließlich 0,5 | NoGo |
| Mindestens ein mittlerer oder hoher Fund, ohne einen der obigen Treffer | GoWithConditions |
| Nur niedrige Funde oder keine Funde | Go |

Ein fehlender EPSS-Wert löst die EPSS-Regel nicht aus. Der Grenzwert ist über
`VERDICT_EPSS_NOGO_THRESHOLD` in Compose einstellbar. Für den gesamten Scan gilt das strengste Urteil.

| Ticket | Start | Erste und zweite Frist |
| --- | --- | --- |
| KEV | Stufe 3, Admin | keine |
| Critical | Stufe 1 | jeweils 4 Stunden |
| High | Stufe 1 | jeweils 24 Stunden |
| Medium | Stufe 1 | jeweils 72 Stunden |
| Low | Stufe 1 | keine |

Beim ersten Prüflauf nach Fristablauf geht das Ticket an den Lead (Stufe 2).
Seine neue Frist beginnt zu diesem Prüfzeitpunkt. Nach deren Ablauf geht es an den
Admin (Stufe 3). Abgeschlossene und risikoakzeptierte Tickets sind ausgenommen.
Scan-API und Worker erhalten die gleichen `ESCALATION_*_HOURS`-Einstellungen.
Manuelle Tickets erhalten ihre Startfrist in der Scan-API, automatische beim Worker.

### Scan-Datenbank einrichten

1. Falls noch keine `.env` vorhanden ist, im Projektordner `.env.example` nach `.env` kopieren
   und die Zugangsdaten für die eigene Testumgebung eintragen.
2. `docker compose up -d --build` ausführen. Bei `MIGRATIONS_RUN=true` (Compose-Standard)
   wendet die Scan-API beim Start die ausstehenden EF-Core-Migrationen für Scans, Tickets
   und Audit an. Bei späteren Starts erkennt EF bereits eingespielte Migrationen.
3. Im Browser http://localhost:8080 öffnen. Die Worker-Meldungen zeigt `docker compose logs worker`.
   Zum Beenden der Dienste genügt `docker compose stop`.

Für eine getrennte Einrichtung des Scan-Schemas zuerst SQL mit `docker compose up -d sql`
starten, `ConnectionStrings__Sims` auf die Ziel-Datenbank setzen und `.\scripts\scan-db.ps1`
ausführen. Vom PC aus lautet die lokale SQL-Adresse `127.0.0.1,1433`, im Container `sql,1433`.
Bei `MIGRATIONS_RUN=false` übernimmt das Deployment die Datenbankeinrichtung für beide APIs
einschließlich der Erstbereitstellung des Admin-Kontos.

Docker lädt lokal `docker-compose.override.yml` automatisch. Damit sind SQL und Redis
vom eigenen PC auf Loopback erreichbar. Für die Bereitstellung nur `-f docker-compose.yml`
angeben; dort laufen SQL und Redis ausschließlich im internen Netz. Dieser Branch verwendet
wie der bisherige Infrastrukturstand das SQL-Volume `sqldata`. Wer bereits Daten aus
`main-test` im Volume `mssql_data` hat, muss beim Wechsel gezielt dieses bestehende
Volume einbinden, bevor Dienste gestartet werden.

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






