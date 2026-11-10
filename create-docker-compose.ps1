# Die versionierte Compose-Datei ist die gemeinsame Konfiguration.
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    if (-not (Test-Path -LiteralPath '.env')) {
        Copy-Item -LiteralPath '.env.example' -Destination '.env'
        Write-Host 'Lokale .env angelegt. Prüfe die Zugangsdaten vor dem Start.'
    }
    docker compose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker-Compose-Konfiguration ungültig.' }
    Write-Host 'Docker-Compose-Konfiguration geprüft.'
} finally {
    Pop-Location
}
