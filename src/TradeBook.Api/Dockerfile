# syntax=docker/dockerfile:1

# Build context is the repository root (see docker-compose.yml) so that
# Directory.Build.props sits above the project exactly as it does on disk.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, on its own layer. Package downloads are only repeated when a
# project file changes, not on every source edit.
COPY Directory.Build.props ./
COPY src/TradeBook.Api/TradeBook.Api.csproj src/TradeBook.Api/
RUN dotnet restore src/TradeBook.Api/TradeBook.Api.csproj

COPY src/TradeBook.Api/ src/TradeBook.Api/
RUN dotnet publish src/TradeBook.Api/TradeBook.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# curl exists only so the compose healthcheck can call /health. The aspnet
# base image ships without it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# The aspnet image defines a non-root user `app` (APP_UID=1654). Kestrel
# listens on 8080 by default in this image (ASPNETCORE_HTTP_PORTS).
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "TradeBook.Api.dll"]
