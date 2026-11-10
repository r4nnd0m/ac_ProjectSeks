ARG RUNTIME=mcr.microsoft.com/dotnet/runtime:10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet publish src/Worker/Worker.csproj -c Release -o /app /p:UseAppHost=false

FROM ${RUNTIME} AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]