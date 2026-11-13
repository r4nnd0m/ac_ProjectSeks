@echo off
rem SIMS-Frontend im Entwicklungsmodus starten (http://localhost:8080).
rem Standard: Demo-Modus mit erfundenen Daten.
rem Gegen die echten APIs (Docker laeuft, Ports 8081-8083): run-dev.bat echt
cd /d %~dp0
set PATH=C:\Program Files\nodejs;%PATH%
if /I "%1"=="echt" (set VITE_USE_MOCKS=false) else (set VITE_USE_MOCKS=true)
call node_modules\.bin\vite.cmd
