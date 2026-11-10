#!/usr/bin/env bash
set -euo pipefail

SLN="sims.slnx"   # Solution-Datei im Repo-Wurzelverzeichnis
OLD="sims"        # Ordner des alten Template-Projekts

[ -f "$SLN" ] && [ -f "$OLD/$OLD.csproj" ] || { echo "Falscher Ordner oder Namen oben anpassen"; exit 1; }

# 1) Deine Program.cs (mit Scalar) sichern, altes Projekt entfernen
cp "$OLD/Program.cs" /tmp/sims-Program.cs
dotnet sln "$SLN" remove "$OLD/$OLD.csproj"
rm -rf "$OLD"

# 2) Die drei APIs: Template, Scalar-Paket, deine Program.cs
mkdir -p src tests
for p in Auth Scan News; do
  dotnet new webapi --use-controllers -n "Sims.$p.Api" -o "src/Sims.$p.Api"
  dotnet add "src/Sims.$p.Api" package Scalar.AspNetCore
  cp /tmp/sims-Program.cs "src/Sims.$p.Api/Program.cs"
done

# 3) Worker, Bibliotheken, Tests
dotnet new worker   -n Sims.Worker  -o src/Sims.Worker
dotnet new classlib -n Sims.Shared  -o src/Sims.Shared
dotnet new classlib -n Sims.Logging -o src/Sims.Logging
dotnet new xunit    -n Sims.Shared.Tests   -o tests/Sims.Shared.Tests
dotnet new xunit    -n Sims.News.Api.Tests -o tests/Sims.News.Api.Tests

# 4) Template-Beispiele löschen
rm -f src/*/Class1.cs tests/*/UnitTest1.cs
rm -f src/*/WeatherForecast.cs src/*/Controllers/WeatherForecastController.cs src/*/*.http

# 5) In die Solution aufnehmen
dotnet sln "$SLN" add src/*/*.csproj   --solution-folder src
dotnet sln "$SLN" add tests/*/*.csproj --solution-folder tests

# 6) Projektreferenzen
dotnet add src/Sims.Scan.Api reference src/Sims.Shared/Sims.Shared.csproj src/Sims.Logging/Sims.Logging.csproj
dotnet add src/Sims.Worker   reference src/Sims.Shared/Sims.Shared.csproj src/Sims.Logging/Sims.Logging.csproj
dotnet add src/Sims.Auth.Api reference src/Sims.Logging/Sims.Logging.csproj
dotnet add tests/Sims.Shared.Tests   reference src/Sims.Shared/Sims.Shared.csproj
dotnet add tests/Sims.News.Api.Tests reference src/Sims.News.Api/Sims.News.Api.csproj

# 7) Dateien im Wurzelverzeichnis
touch docker-compose.yml .env.example README.md
printf '\n.idea/\n.env\n' >> .gitignore

cat > Directory.Build.props <<'EOF'
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
EOF

# 8) Zentrale Paketversionen: Versionen aus den .csproj nach Directory.Packages.props verschieben
grep -rhoE --include=*.csproj '<PackageReference Include="[^"]+" Version="[^"]+"' src tests \
  | sed -E 's|<PackageReference Include="([^"]+)" Version="([^"]+)"|<PackageVersion Include="\1" Version="\2" />|' \
  | sort -u > /tmp/pkgversions.txt

dups=$(cut -d'"' -f2 /tmp/pkgversions.txt | sort | uniq -d)
[ -z "$dups" ] || { echo "Versionskonflikt bei: $dups"; exit 1; }

find src tests -name '*.csproj' -exec perl -pi -e 's/(<PackageReference Include="[^"]+") Version="[^"]+"/$1/' {} +

{
  echo '<Project>'
  echo '  <PropertyGroup>'
  echo '    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>'
  echo '  </PropertyGroup>'
  echo '  <ItemGroup>'
  sed 's/^/    /' /tmp/pkgversions.txt
  echo '  </ItemGroup>'
  echo '</Project>'
} > Directory.Packages.props

# 9) Solution Items in die .slnx eintragen
perl -0pi -e 's#</Solution>#  <Folder Name="/Solution Items/">\n    <File Path="docker-compose.yml" />\n    <File Path=".env.example" />\n    <File Path="Directory.Build.props" />\n    <File Path="Directory.Packages.props" />\n    <File Path="README.md" />\n  </Folder>\n</Solution>#' "$SLN"

# 10) Prüfen
dotnet build "$SLN"
echo "--- Scalar in den Program.cs (sollte 3 Treffer geben):"
grep -n MapScalarApiReference src/*/Program.cs