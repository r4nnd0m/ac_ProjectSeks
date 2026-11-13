# Lokal anwenden:  .\scripts\scan-db.ps1
# SQL exportieren: .\scripts\scan-db.ps1 -OutputSql .\scan-migration.sql
[CmdletBinding()]
param([string]$OutputSql)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if ($OutputSql) {
    $OutputSql = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputSql)
}

Push-Location -LiteralPath $repo
try {
    dotnet build src/ScanApi/ScanApi.csproj --verbosity quiet --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Die Scan-API konnte nicht gebaut werden.' }

    $ef = @('--project', 'src/Sims.Shared', '--startup-project', 'src/ScanApi',
            '--context', 'SimsDbContext', '--no-build', '--no-color')
    $list = dotnet ef migrations list --no-connect --json @ef
    if ($LASTEXITCODE -ne 0) { throw 'EF konnte die Scan-Migrationen nicht auflisten. Prüfe dotnet ef --version.' }
    $migrations = ($list -join [Environment]::NewLine) | ConvertFrom-Json
    if (-not $migrations) {
        throw 'Keine EF-Migration für SimsDbContext gefunden.'
    }
    dotnet ef migrations has-pending-model-changes @ef
    if ($LASTEXITCODE -ne 0) { throw 'Das Modell hat Änderungen ohne Migration. Zuerst dotnet ef migrations add ausführen.' }

    if ($OutputSql) {
        dotnet ef migrations script --idempotent --output $OutputSql @ef
        if ($LASTEXITCODE -ne 0) { throw 'Der SQL-Export ist fehlgeschlagen.' }
        Write-Host "SQL-Skript erstellt: $OutputSql"
    } else {
        if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Sims)) {
            throw 'Für diesen Aufruf ConnectionStrings__Sims auf die Ziel-Datenbank setzen. Die Variable wird nicht ausgegeben.'
        }
        dotnet ef database update @ef
        if ($LASTEXITCODE -ne 0) { throw 'Das Datenbank-Update ist fehlgeschlagen.' }
    }
} finally {
    Pop-Location
}
