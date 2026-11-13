<p align="center">
  <img src="CMSLPD-Schild-Pixel.png" width="152" height="152" alt="CMSLPD – blaues Pixelschild mit Kristall">
</p>

<h1 align="center">SIMS · Sul Sul, Exploit!</h1>
<p align="center">Security Incident Management System · FH-Projekt Advanced Coding</p>

SIMS hilft einem Team bei der Entscheidung, ob es eine Softwareversion freigeben kann. Du lädst eine Liste der verwendeten Softwarepakete hoch. Der Worker prüft die Paketversionen auf bekannte Schwachstellen. Im Browser siehst du das Urteil, die einzelnen Funde und die Tickets, um die sich das Team kümmern muss.

**Welche Version starte ich?** Die folgende Anleitung beschreibt den integrierten Teamstand auf dem [Frontend-Design-Branch](https://github.com/r4nnd0m/ac_ProjectSeks/tree/bereinigtes-backend-based-on-frontend-design). Dort laufen Weboberfläche, APIs und Worker gemeinsam. Auf `main` kannst du den Worker separat mit vorbereiteten Testdaten ausprobieren. [Anleitung zur Offline-Demo](docs/worker/DEMO_Anleitung.txt)

[Lokaler Start](#lokaler-start) · [Erster Scan](#erster-scan) · [Urteile und Tickets](#urteile-und-tickets) · [Projektaufbau](#projektaufbau) · [Entwicklung und Übergaben](#entwicklung-und-übergaben)

## Lokaler Start

Du brauchst Git und ein laufendes Docker Desktop mit Linux-Containern. Öffne PowerShell im Projektordner des Frontend-Design-Branches. Dort liegt `docker-compose.yml`. Docker bringt die Laufzeit für das Frontend, die .NET-Dienste, SQL Server und Redis mit.

**Wenn SIMS auf diesem PC schon eingerichtet ist**, verwende denselben Projektordner und seine vorhandene `.env`.

```powershell
docker compose up -d --no-build
```

Öffne danach [SIMS auf localhost:8080](http://localhost:8080). Melde dich mit `ADMIN_EMAIL` und `ADMIN_PASSWORD` aus deiner lokalen `.env` an. Bei aktivierter Zwei-Faktor-Anmeldung gibst du zusätzlich den aktuellen sechsstelligen Code aus deiner Authenticator-App ein. Zum Einrichten öffnest du nach der Anmeldung **2FA**, scannst den QR-Code mit der App und bestätigst mit ihrem aktuellen Code.

<details>
<summary><strong>Erster Start auf einem neuen PC</strong></summary>

1. Hole die Teamversion und wechsle in den Projektordner.

   ```powershell
   git clone --branch bereinigtes-backend-based-on-frontend-design https://github.com/r4nnd0m/ac_ProjectSeks.git sims
   cd sims
   ```

2. Erstelle die lokale Konfiguration und öffne sie.

   ```powershell
   Copy-Item .env.example .env
   notepad .env
   ```

   Trage deine Werte für `MSSQL_SA_PASSWORD`, `REDIS_PASSWORD`, `JWT_KEY`, `ADMIN_EMAIL` und `ADMIN_PASSWORD` ein. Verwende für das Admin-Passwort mindestens zwölf Zeichen mit Groß- und Kleinbuchstaben, Zahl und Sonderzeichen. Der JWT-Schlüssel benötigt mindestens 32 Zeichen. Lass `MIGRATIONS_RUN=true` für den ersten Start gesetzt. Damit richten die APIs ihre Tabellen ein und die Auth-API legt das erste Admin-Konto an.

3. Baue und starte die Dienste.

   ```powershell
   docker compose up -d --build
   ```

   **Bekannte Besonderheit auf diesem Branch (Stand `abe8b59`)** · Beim Bau des Frontends meldet TypeScript `TS6133` oder `TS6192` für unbenutzte Variablen und Importe. Falls dein Build daran stoppt, führe diesen Block im Projektordner aus. Er schaltet diese Prüfung nur für das lokale Web-Image aus und baut die übrigen Dienste regulär.

   ```powershell
   $webImage = docker compose config --images | Where-Object { $_ -match '-web(:latest)?$' }
   $buildStep = 'RUN sed -i ''/noUnusedLocals/s/true/false/'' tsconfig.app.json && npm run build'
   (Get-Content -Raw src/Web/Dockerfile).Replace('RUN npm run build', $buildStep) | docker build -f - -t $webImage ./src/Web
   docker compose build auth-api scan-api news-api worker
   docker compose up -d --no-build
   ```

4. Mit `docker compose ps -a` siehst du den Startzustand. SQL und Redis melden `healthy`. Für `sql-init` bedeutet `Exited (0)`, dass die einmalige Vorbereitung erfolgreich war. Sobald die Anmeldung bereit ist, öffne [SIMS im Browser](http://localhost:8080).

</details>

## Erster Scan

Eine **SBOM** (*Software Bill of Materials*) ist die Stückliste einer Softwareversion. Sie enthält zum Beispiel den Namen einer Bibliothek, ihre Version und ihre Paketkennung. Für den ersten Durchlauf bringt SIMS eine solche Liste mit.

1. **Projekt anlegen.** Öffne **Projekte**, gib unter **Neues Projekt anlegen** einen Namen wie „Kassensystem“ ein und klicke auf **Projekt anlegen**.
2. **Stückliste auswählen.** Klicke auf der Projektseite auf **Beispiel-SBOM einfügen**. Trage unter **Versionsangabe** die Version deines Projekts ein, zum Beispiel `1.0.0`. Für eigene Daten kannst du eine CycloneDX-JSON-Datei hochladen (bis zu 5 MB).
3. **Scan starten.** Die Ergebnisseite aktualisiert sich automatisch. **Wartend** heißt, dass der Auftrag gespeichert ist. Der Worker schaut standardmäßig alle 30 Sekunden nach neuen Aufträgen. Während der Prüfung zeigt die Seite **Läuft**.
4. **Ergebnis lesen.** Unter **Abgeschlossen** siehst du Urteil und Funde. Unter **Tickets** findest du die daraus entstandenen Aufgaben. Das Ergebnis der Beispiel-SBOM hängt von den aktuellen öffentlichen Schwachstellendaten ab. Bei **Fehlgeschlagen** hilft `docker compose logs --tail 80 worker` bei der Fehlersuche.

## Urteile und Tickets

Für die gesamte Softwareversion gilt die strengste zutreffende Regel. Die Tabelle zeigt die Standardwerte. Den EPSS-Grenzwert legt das Team über `VERDICT_EPSS_NOGO_THRESHOLD` fest.

| Urteil im Browser | Wann es gilt |
| --- | --- |
| **NoGo – Freigabe gesperrt** | Mindestens ein kritischer Fund, ein Eintrag im CISA-Katalog bekanntermaßen ausgenutzter Schwachstellen (KEV) oder ein EPSS-Wert ab 0,5. EPSS schätzt, wie wahrscheinlich eine Ausnutzung in den nächsten 30 Tagen ist. |
| **Go mit Auflagen** | Mindestens ein mittlerer oder hoher Fund und keine NoGo-Bedingung. |
| **Go – Freigabe möglich** | Nur niedrige Funde oder keine Funde. |

Ab **mittlerer Schwere** sowie für jeden **KEV-Fund** erstellt SIMS ein Ticket. Ein erneuter Scan erzeugt für dieselbe Schwachstelle in derselben Komponente desselben Projekts kein zweites Ticket, solange das vorhandene Ticket noch nicht geschlossen ist.

| Ticket | Erste Zuständigkeit | Was bei Fristablauf passiert |
| --- | --- | --- |
| Kritisch / hoch / mittel | Stufe 1 | Nach 4 / 24 / 72 Stunden weist der Worker das offene Ticket beim nächsten Prüflauf der Rolle **Lead** zu (Stufe 2). Nach einer weiteren gleich langen Frist geht es an **Admin** (Stufe 3). |
| KEV-Fund | Sofort **Admin** (Stufe 3) | Keine weitere Frist. |
| Niedrig | Stufe 1 | Keine automatische Eskalation. |

Die zugewiesene Rolle steht am Ticket. Ein kritisches Ticket ohne KEV-Eintrag beginnt zum Beispiel auf Stufe 1. Bleibt es offen, geht es nach rund vier Stunden an den Lead und nach weiteren vier Stunden an den Admin. Der Verlauf lässt sich im Audit-Protokoll nachvollziehen. Geschlossene Tickets und Tickets mit akzeptiertem Risiko werden nicht weiter eskaliert.

## Projektaufbau

| Baustein | Aufgabe |
| --- | --- |
| [Weboberfläche](https://github.com/r4nnd0m/ac_ProjectSeks/tree/bereinigtes-backend-based-on-frontend-design/src/Web) | React und TypeScript zeigen Projekte, Scans, Tickets, Verwaltung und öffentliche Meldungen. Der Browser erreicht alle APIs über Port **8080**. |
| [Auth-API](https://github.com/r4nnd0m/ac_ProjectSeks/tree/bereinigtes-backend-based-on-frontend-design/src/AuthApi) | Anmeldung, Zwei-Faktor-Anmeldung, Rollen, Benutzer und Teams. Lokal direkt auf Port **8081**. |
| [Scan-API](https://github.com/r4nnd0m/ac_ProjectSeks/tree/bereinigtes-backend-based-on-frontend-design/src/ScanApi) | Nimmt SBOMs an, speichert Scanaufträge und liefert Projekte, Ergebnisse, Tickets und Audit-Einträge. Port **8082**. |
| [Worker](https://github.com/r4nnd0m/ac_ProjectSeks/tree/bereinigtes-backend-based-on-frontend-design/src/Worker) | Holt wartende Aufträge aus SQL, fragt OSV nach Schwachstellen, ergänzt KEV- und EPSS-Daten und speichert Urteil und Tickets. Ein zweiter Hintergrundjob prüft die Ticketfristen. |
| [News-API](https://github.com/r4nnd0m/ac_ProjectSeks/tree/bereinigtes-backend-based-on-frontend-design/src/NewsApi) | Liest öffentliche Sicherheitsmeldungen aus den konfigurierten Feeds. Port **8083**. |
| [SQL Server und Redis](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/docker-compose.yml) | SQL Server speichert die fachlichen Daten dauerhaft. Redis sammelt unter anderem technische Logmeldungen. |

## Entwicklung und Übergaben

| Aufgabe | Befehl im Projektordner |
| --- | --- |
| Status aller Dienste anzeigen | `docker compose ps -a` |
| Worker während eines Scans beobachten | `docker compose logs -f worker` (Anzeige mit Strg+C beenden) |
| Dienste stoppen und Daten behalten | `docker compose stop` |
| Vorhandene Images erneut starten | `docker compose up -d --no-build` |
| Backend-Tests mit dem .NET 10 SDK ausführen | `dotnet test sims.slnx` |

<details>
<summary><strong>Datenbank und Deployment</strong></summary>

Beim Start mit `MIGRATIONS_RUN=true` wenden die APIs ihre ausstehenden EF-Core-Migrationen an. Wer das Scan-Schema getrennt ausrollen möchte, verwendet das Skript mit dem .NET 10 SDK und `dotnet ef` 10.

- **Lokal anwenden.** Starte SQL mit `docker compose up -d sql`, setze `ConnectionStrings__Sims` in der aktuellen PowerShell auf die Zieldatenbank und führe `.\scripts\scan-db.ps1` aus.
- **SQL für ein Deployment erzeugen.** `.\scripts\scan-db.ps1 -OutputSql .\scan-migration.sql` erzeugt ein Skript, das bereits eingespielte Migrationen überspringt. Bei `MIGRATIONS_RUN=false` übernimmt das Deployment die Einrichtung beider APIs und des ersten Admin-Kontos.
- **Vorhandene Daten übernehmen.** Dieser Branch verwendet das Docker-Volume `sqldata`. Der frühere Stand auf `main-test` verwendete `mssql_data`. Prüfe beim Wechsel den Volume-Namen und den Compose-Projektnamen, damit du die gewünschte Datenbank öffnest.
- **Lokaler Datenbankzugriff.** Durch die lokale Compose-Ergänzung ist SQL vom PC aus unter `127.0.0.1,1433` erreichbar, im Container unter `sql,1433`. Beim Deployment lädt `docker compose -f docker-compose.yml up -d` nur die Basisdatei.

[Werkzeug für Scan-Migrationen](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/scripts/scan-db.ps1) · [Lokale Portfreigaben](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/docker-compose.override.yml)

</details>

<details>
<summary><strong>Arbeitspakete und Ansprechpartner</strong></summary>

| Bereich | Übergaben |
| --- | --- |
| Stefan · Worker | [D1 Logging](docs/worker/D1_Logging_Uebergabe.txt) · [D2 OSV](docs/worker/D2_OSV_Uebergabe.txt) · [D3 Scanablauf](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/docs/worker/D3_Scanablauf_Uebergabe.txt) · [D4 Eskalation](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/docs/worker/D4_Eskalation_Uebergabe.txt) |
| Patrick · Scan-API und Security-Basis | [B1 Datenmodell](docs/worker/abstimmung/B1_Datenmodell.txt) · [B2 Upload](docs/worker/abstimmung/B2_SBOM_Upload.txt) · [B3 Projekte und Teams](docs/worker/abstimmung/B3_Projekt_und_Team.txt) · [B5 Scanergebnis](docs/worker/abstimmung/B5_Scanergebnis_lesen.txt) |
| Christian · Regeln und Infrastruktur | [B4 Scanurteil](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/docs/worker/abstimmung/B4_Scanurteil.txt) · [C4 Eskalationsregeln](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/docs/worker/abstimmung/C4_Eskalationsregeln.txt) · [F2 Compose](docs/worker/abstimmung/F2_Compose.txt) · [F3 Worker-Image](docs/worker/abstimmung/F3_Workerimage.txt) |
| Leo · Tickets und Audit | [C1 Ticketmodell](docs/worker/abstimmung/C1_Ticketmodell.txt) · [C2 Ticket-API](docs/worker/abstimmung/C2_Ticket_API.txt) · [C3 Automatische Tickets](docs/worker/abstimmung/C3_Automatische_Tickets.txt) · [C5 Audit](docs/worker/abstimmung/C5_Audit.txt) |

[Gemeinsame Worker-Abstimmung](docs/worker/ABSTIMMUNG_TEAM_WORKER.txt) · [Entwicklungsanleitung für das Frontend](https://github.com/r4nnd0m/ac_ProjectSeks/blob/bereinigtes-backend-based-on-frontend-design/src/Web/README.md)

</details>
