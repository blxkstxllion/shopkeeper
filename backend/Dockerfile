# syntax=docker/dockerfile:1

# ---- base: shared restore layer -------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
WORKDIR /src

COPY ShopKeeper.sln ./
COPY src/ShopKeeper.Domain/ShopKeeper.Domain.csproj src/ShopKeeper.Domain/
COPY src/ShopKeeper.Application/ShopKeeper.Application.csproj src/ShopKeeper.Application/
COPY src/ShopKeeper.Infrastructure/ShopKeeper.Infrastructure.csproj src/ShopKeeper.Infrastructure/
COPY src/ShopKeeper.Api/ShopKeeper.Api.csproj src/ShopKeeper.Api/
COPY tests/ShopKeeper.Api.Tests/ShopKeeper.Api.Tests.csproj tests/ShopKeeper.Api.Tests/
RUN dotnet restore ShopKeeper.sln

COPY . .

# ---- development: dotnet watch, source bind-mounted by docker-compose.dev.yml ----
FROM base AS development
# So `docker compose exec api dotnet ef ...` (see database/README.md) works without
# also requiring the EF Core tool on the host. curl is for the HEALTHCHECK below - the
# dev overlay's `frontend` service depends on `api: condition: service_healthy`, which
# hangs forever if this stage has no HEALTHCHECK at all (Docker reports status "none",
# which never satisfies "healthy").
RUN dotnet tool install --global dotnet-ef --version 8.* \
    && apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="${PATH}:/root/.dotnet/tools"
WORKDIR /src/src/ShopKeeper.Api
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=60s --retries=5 \
    CMD curl -f http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://0.0.0.0:8080"]

# ---- build: compile + publish for production -------------------------------
FROM base AS build
WORKDIR /src/src/ShopKeeper.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- final: minimal ASP.NET runtime image, non-root ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN addgroup --system --gid 1000 shopkeeper \
    && adduser --system --uid 1000 --ingroup shopkeeper --shell /bin/false shopkeeper \
    && apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=shopkeeper:shopkeeper /app/publish .

USER shopkeeper
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ShopKeeper.Api.dll"]
