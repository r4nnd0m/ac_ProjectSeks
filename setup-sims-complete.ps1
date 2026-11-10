<#
.SYNOPSIS
    Vollständiges Setup-Skript für die SIMS-Projektmappe und Ordnerstruktur.
#>

$ErrorActionPreference = "Stop"

Write-Host "🚀 Starte vollständiges SIMS Projekt- und Infrastruktur-Setup..." -ForegroundColor Cyan

# 1. Verzeichnisse anlegen
$dirs = @(
    "src/Sims.Contracts",
    "src/Sims.Shared",
    "src/Sims.Logging",
    "src/AuthApi",
    "src/ScanApi",
    "src/NewsApi",
    "src/Worker",
    "web"
)

foreach ($dir in $dirs) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
}
Write-Host "✅ Ordnerstruktur erstellt." -ForegroundColor Green


# 2. .NET Solution erstellen, falls nicht vorhanden
if (!(Test-Path "sims.slnx")) {
    dotnet new sln -n sims --output . | Out-Null
    Write-Host "✅ Solution-Datei erstellt." -ForegroundColor Green
}


# 3. Basis-Bibliotheken und APIs initialisieren
if (!(Test-Path "src/Sims.Contracts/Sims.Contracts.csproj")) {
    dotnet new classlib -n Sims.Contracts -o src/Sims.Contracts -f net10.0 | Out-Null
}
if (!(Test-Path "src/Sims.Shared/Sims.Shared.csproj")) {
    dotnet new classlib -n Sims.Shared -o src/Sims.Shared -f net10.0 | Out-Null
}
if (!(Test-Path "src/Sims.Logging/Sims.Logging.csproj")) {
    dotnet new classlib -n Sims.Logging -o src/Sims.Logging -f net10.0 | Out-Null
}
if (!(Test-Path "src/AuthApi/AuthApi.csproj")) {
    dotnet new webapi -n AuthApi -o src/AuthApi -f net10.0 | Out-Null
}
if (!(Test-Path "src/ScanApi/ScanApi.csproj")) {
    dotnet new webapi -n ScanApi -o src/ScanApi -f net10.0 | Out-Null
}
if (!(Test-Path "src/NewsApi/NewsApi.csproj")) {
    dotnet new webapi -n NewsApi -o src/NewsApi -f net10.0 | Out-Null
}
if (!(Test-Path "src/Worker/Worker.csproj")) {
    dotnet new worker -n Worker -o src/Worker -f net10.0 | Out-Null
}

Write-Host "✅ Alle .NET Projekte initialisiert." -ForegroundColor Green


# 4. Projekte zur Solution hinzufügen
$projects = Get-ChildItem -Path "src" -Recurse -Filter "*.csproj"
foreach ($proj in $projects) {
    dotnet sln sims.sln add $proj.FullName | Out-Null
}
Write-Host "✅ Alle Projekte zur Solution hinzugefügt." -ForegroundColor Green

Write-Host "🎉 Das .NET-Setup ist abgeschlossen!" -ForegroundColor Cyan