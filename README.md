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


 
