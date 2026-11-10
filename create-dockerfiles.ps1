<#
.SYNOPSIS
    Erstellt alle Dockerfile-Dateien für die SIMS-Infrastruktur.
#>

$ErrorActionPreference = "Stop"

Write-Host "🚀 Erstelle Dockerfiles für alle Services..." -ForegroundColor Cyan

# 1. AuthApi Dockerfile
$authApiContent = @"
ARG RUNTIME=mcr.microsoft.com/dotnet/aspnet:10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet publish src/AuthApi/AuthApi.csproj -c Release -o /app /p:UseAppHost=false

FROM `${RUNTIME} AS final
WORKDIR /app
COPY --from=build /app .
USER `$APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AuthApi.dll"]
"@
Set-Content -Path "src/AuthApi/Dockerfile" -Value $authApiContent -Encoding utf8


# 2. ScanApi Dockerfile
$scanApiContent = @"
ARG RUNTIME=mcr.microsoft.com/dotnet/aspnet:10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet publish src/ScanApi/ScanApi.csproj -c Release -o /app /p:UseAppHost=false

FROM `${RUNTIME} AS final
WORKDIR /app
COPY --from=build /app .
USER `$APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ScanApi.dll"]
"@
Set-Content -Path "src/ScanApi/Dockerfile" -Value $scanApiContent -Encoding utf8


# 3. NewsApi Dockerfile
$newsApiContent = @"
ARG RUNTIME=mcr.microsoft.com/dotnet/aspnet:10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet publish src/NewsApi/NewsApi.csproj -c Release -o /app /p:UseAppHost=false

FROM `${RUNTIME} AS final
WORKDIR /app
COPY --from=build /app .
USER `$APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NewsApi.dll"]
"@
Set-Content -Path "src/NewsApi/Dockerfile" -Value $newsApiContent -Encoding utf8


# 4. Worker Dockerfile
$workerContent = @"
ARG RUNTIME=mcr.microsoft.com/dotnet/runtime:10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet publish src/Worker/Worker.csproj -c Release -o /app /p:UseAppHost=false

FROM `${RUNTIME} AS final
WORKDIR /app
COPY --from=build /app .
USER `$APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]
"@
Set-Content -Path "src/Worker/Dockerfile" -Value $workerContent -Encoding utf8


# 5. Web Frontend Dockerfile
$webDockerfileContent = @"
FROM node:lts-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:stable-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
"@
Set-Content -Path "web/Dockerfile" -Value $webDockerfileContent -Encoding utf8

Write-Host "✅ Alle Dockerfiles wurden erfolgreich erstellt!" -ForegroundColor Green