# The Shop Keeper

**Know Your Business. Grow Your Profit.**

The intelligent business operating system for shops and growing businesses — not just a POS.

## Architecture

- **Frontend**: React + TypeScript + Vite + Tailwind CSS + React Router + TanStack Query + React Hook Form + Zod + Recharts
- **Backend**: ASP.NET Core Web API (C#) with clean architecture (`Api` / `Application` / `Domain` / `Infrastructure`), MediatR (CQRS), FluentValidation
- **Database**: PostgreSQL via Entity Framework Core, multi-tenant with automatic tenant-isolation query filters
- **Auth**: JWT access tokens + rotating, revocable refresh tokens (httpOnly cookie)

## Repo layout

```
backend/
  src/
    ShopKeeper.Api/             thin controllers, DI wiring, middleware
    ShopKeeper.Application/     CQRS commands/queries, validation, DTOs
    ShopKeeper.Domain/          entities, enums, constants (no external deps)
    ShopKeeper.Infrastructure/  EF Core, identity, external services
  tests/
    ShopKeeper.Api.Tests/
frontend/                       React app (Vite)
infra/
  docker-compose.yml            local PostgreSQL
```

## Prerequisites

- .NET SDK 8
- Node.js 20+
- Docker Desktop (for local PostgreSQL)

## First-time setup

1. Start the database:
   ```bash
   cd infra
   docker compose up -d
   ```

2. Apply migrations:
   ```bash
   cd backend
   dotnet ef database update \
     --project src/ShopKeeper.Infrastructure/ShopKeeper.Infrastructure.csproj \
     --startup-project src/ShopKeeper.Api/ShopKeeper.Api.csproj
   ```

3. Run the backend (Swagger at `http://localhost:5064/swagger`):
   ```bash
   cd backend
   dotnet run --project src/ShopKeeper.Api
   ```

4. Run the frontend:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```

The frontend dev server (`http://localhost:5173`) and API CORS policy are pre-wired to talk to each other in `appsettings.Development.json`.

## Note on the local database port

`infra/docker-compose.yml` maps PostgreSQL to host port **5433**, not the default 5432. If your machine has any other PostgreSQL install (native service, another project's container, etc.) already bound to 5432, Docker's forwarding and that instance silently collide and the app ends up talking to the wrong database with the wrong credentials (`password authentication failed` is the symptom). `Port=5433` is set to match in `appsettings.Development.json` — if you're certain nothing else on your machine uses 5432, you can change both back, but there's no need to.

## Configuration

`backend/src/ShopKeeper.Api/appsettings.json` holds only placeholders for `ConnectionStrings:Default` and `Jwt:Secret` — these must come from environment variables (`ConnectionStrings__Default`, `Jwt__Secret`) in any non-development environment. Local development values live in `appsettings.Development.json` (dev-only credentials, safe to commit).

## Status

Currently in **Phase 1** of the build (see the master spec): authentication, business onboarding, roles/permissions, and navigation shell. See later phases for POS, inventory, profitability engine, multi-branch, AI consultant, etc.
