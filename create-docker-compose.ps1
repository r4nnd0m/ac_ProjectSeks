<#
.SYNOPSIS
    Erstellt die docker-compose.yml und die .env-Datei für das SIMS-Projekt.
#>

$ErrorActionPreference = "Stop"

Write-Host "🚀 Erstelle Docker-Compose-Infrastruktur..." -ForegroundColor Cyan

# 1. .env Datei erstellen
$envContent = @'
SQL_PASSWORD=SecureCoding123!SecureCoding123!
REDIS_PASSWORD=SecureCoding123!SecureCoding123!
JWT_KEY=super-secret-key-with-at-least-32-characters-long
JWT_ISSUER=sims-auth
JWT_AUDIENCE=sims-api
JWT_EXPIRES_MINUTES=15
ADMIN_EMAIL=admin@sims.local
ADMIN_PASSWORD=SecureCoding123!SecureCoding123!
CORS_ALLOWED_ORIGIN=http://localhost:8080
FEED_URL_1=https://www.cert.at/cert-at.de.warnings.rss_2.0.xml
FEED_URL_2=https://github.blog/feed/
'@
Set-Content -Path ".env" -Value $envContent -Encoding utf8
Write-Host "✅ .env Datei erfolgreich erstellt." -ForegroundColor Green


# 2. docker-compose.yml erstellen (inkl. sql-init und korrigiertem Healthcheck)
$composeContent = @'
services:
  sql-init:
    image: busybox:latest
    container_name: sql-init
    user: root
    command: ["sh", "-c", "mkdir -p /var/opt/mssql/data /var/opt/mssql/log /var/opt/mssql/secrets && chown -R 10001:0 /var/opt/mssql"]
    volumes:
      - sqldata:/var/opt/mssql
    restart: "no"

  sql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${SQL_PASSWORD}
    volumes:
      - sqldata:/var/opt/mssql
    networks:
      - backend-net
    depends_on:
      sql-init:
        condition: service_completed_successfully
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    restart: unless-stopped

  redis:
    image: redis:7.2-alpine
    command: ["redis-server", "--requirepass", "${REDIS_PASSWORD}"]
    networks:
      - backend-net
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
    restart: unless-stopped

  auth-api:
    build:
      context: .
      dockerfile: src/AuthApi/Dockerfile
    environment:
      - ConnectionStrings__Auth=Server=sql,1433;Database=sims;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True
      - Jwt__Key=${JWT_KEY}
      - Jwt__Issuer=${JWT_ISSUER}
      - Jwt__Audience=${JWT_AUDIENCE}
      - Jwt__ExpiresMinutes=${JWT_EXPIRES_MINUTES}
      - Admin__Email=${ADMIN_EMAIL}
      - Admin__Password=${ADMIN_PASSWORD}
      - Cors__AllowedOrigins__0=${CORS_ALLOWED_ORIGIN}
      - Redis__ConnectionString=redis:6379,password=${REDIS_PASSWORD}
    networks:
      - frontend-net
      - backend-net
    ports:
      - "8081:8080"
    depends_on:
      sql:
        condition: service_healthy
      redis:
        condition: service_healthy
    security_opt:
      - no-new-privileges:true
    cap_drop:
      - ALL
    restart: unless-stopped

  scan-api:
    build:
      context: .
      dockerfile: src/ScanApi/Dockerfile
    environment:
      - ConnectionStrings__Sims=Server=sql,1433;Database=sims;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True
      - Jwt__Key=${JWT_KEY}
      - Jwt__Issuer=${JWT_ISSUER}
      - Jwt__Audience=${JWT_AUDIENCE}
      - Cors__AllowedOrigins__0=${CORS_ALLOWED_ORIGIN}
      - Redis__ConnectionString=redis:6379,password=${REDIS_PASSWORD}
    networks:
      - frontend-net
      - backend-net
    ports:
      - "8082:8080"
    depends_on:
      sql:
        condition: service_healthy
      redis:
        condition: service_healthy
    security_opt:
      - no-new-privileges:true
    cap_drop:
      - ALL
    restart: unless-stopped

  news-api:
    build:
      context: .
      dockerfile: src/NewsApi/Dockerfile
    environment:
      - Feeds__Urls__0=${FEED_URL_1}
      - Feeds__Urls__1=${FEED_URL_2}
      - Cors__AllowedOrigins__0=${CORS_ALLOWED_ORIGIN}
      - Redis__ConnectionString=redis:6379,password=${REDIS_PASSWORD}
    networks:
      - frontend-net
    ports:
      - "8083:8080"
    depends_on:
      redis:
        condition: service_healthy
    security_opt:
      - no-new-privileges:true
    cap_drop:
      - ALL
    restart: unless-stopped

  worker:
    build:
      context: .
      dockerfile: src/Worker/Dockerfile
    environment:
      - ConnectionStrings__Sims=Server=sql,1433;Database=sims;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True
      - Redis__ConnectionString=redis:6379,password=${REDIS_PASSWORD}
      - Worker__PollIntervalSeconds=30
    networks:
      - backend-net
      - egress-net
    depends_on:
      sql:
        condition: service_healthy
      redis:
        condition: service_healthy
    security_opt:
      - no-new-privileges:true
    cap_drop:
      - ALL
    restart: unless-stopped

  web:
    build:
      context: ./web
      dockerfile: Dockerfile
    networks:
      - frontend-net
    ports:
      - "8080:8080"
    restart: unless-stopped

volumes:
  sqldata:

networks:
  frontend-net:
    driver: bridge
  backend-net:
    internal: true
    driver: bridge
  egress-net:
    driver: bridge
'@

Set-Content -Path "docker-compose.yml" -Value $composeContent -Encoding utf8
Write-Host "✅ docker-compose.yml erfolgreich erstellt." -ForegroundColor Green

Write-Host "🎉 Setup der Docker-Umgebung abgeschlossen!" -ForegroundColor Cyan