@echo off
rem SIMS-Frontend im Demo-Modus starten (Port 8080).
cd /d %~dp0
set PATH=C:\Program Files\nodejs;%PATH%
set VITE_USE_MOCKS=true
call node_modules\.bin\vite.cmd > vite-dev.log 2>&1
