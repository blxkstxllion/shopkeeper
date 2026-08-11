# Database

Schema is owned entirely by EF Core migrations in
[`backend/src/ShopKeeper.Infrastructure/Persistence/Migrations`](../backend/src/ShopKeeper.Infrastructure/Persistence/Migrations) -
that is the single source of truth. Nothing in this directory defines schema, and
production schema should never be edited by hand.

This directory holds database-adjacent things that aren't C# migrations:

- **`init/`** - scripts Postgres's official image auto-runs, once, only against a
  brand-new (empty) data volume, via
  [`docker-entrypoint-initdb.d`](https://hub.docker.com/_/postgres) (wired up in
  `docker/docker-compose.yml`). Use this only for things that must exist before
  any migration runs (extensions, roles) - not for schema or seed data.
- **Seed data** for local development lives in the backend instead (an
  `ISeeder`/startup hook under `ShopKeeper.Infrastructure`, run explicitly - see
  the root README's "Seed development data" section), so it can use the same
  EF Core entities and stay in sync with the model automatically.

## Running migrations

Always from `backend/`, against whichever database `ConnectionStrings__Default`
(or `appsettings.Development.json` locally) points at:

```bash
cd backend
dotnet ef database update \
  --project src/ShopKeeper.Infrastructure/ShopKeeper.Infrastructure.csproj \
  --startup-project src/ShopKeeper.Api/ShopKeeper.Api.csproj
```

Creating a new migration after changing entities/configurations:

```bash
dotnet ef migrations add <Name> \
  --project src/ShopKeeper.Infrastructure/ShopKeeper.Infrastructure.csproj \
  --startup-project src/ShopKeeper.Api/ShopKeeper.Api.csproj
```

**Production migrations are not automatic.** Nothing in the Docker images or CI/CD
pipeline runs `database update` against production on deploy - that is a deliberate,
reviewed, manual step (see the root README's deployment section) so a bad migration
can never auto-apply to production data.
