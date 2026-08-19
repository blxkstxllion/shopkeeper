# The Shop Keeper

**Know Your Business. Grow Your Profit.**

The intelligent business operating system for shops and growing businesses — not just a POS.

## Architecture

- **Frontend**: React + TypeScript + Vite + Tailwind CSS + React Router + TanStack Query + React Hook Form + Zod + Recharts — built and served via Nginx in containers.
- **Backend**: ASP.NET Core Web API (C#) with clean architecture (`Api` / `Application` / `Domain` / `Infrastructure`), MediatR (CQRS), FluentValidation.
- **Database**: PostgreSQL via Entity Framework Core, multi-tenant with automatic tenant-isolation query filters.
- **Cache/queue**: Redis, containerized and available to every environment. Not currently consumed by any feature — see `AddInfrastructure` in `ShopKeeper.Infrastructure` for the (inert until configured) wiring point.
- **Auth**: JWT access tokens + rotating, revocable refresh tokens (httpOnly cookie).
- **Containerization**: Docker multi-stage builds for both apps; Docker Compose for local dev, staging, and production.
- **CI/CD**: GitHub Actions → GitHub Container Registry (GHCR) → staging → production, with immutable versioned images promoted (not rebuilt) from staging to production.

## Repo layout

```
backend/
  Dockerfile, .dockerignore
  src/
    ShopKeeper.Api/             thin controllers, DI wiring, middleware
    ShopKeeper.Application/     CQRS commands/queries, validation, DTOs
    ShopKeeper.Domain/          entities, enums, constants (no external deps)
    ShopKeeper.Infrastructure/  EF Core, identity, external services
  tests/
    ShopKeeper.Api.Tests/
frontend/
  Dockerfile, .dockerignore, nginx.conf
  src/                          React app (Vite)
database/
  README.md                     how migrations/seeding work; not the migrations themselves
  init/                         Postgres first-boot-only init scripts (extensions, roles)
docker/
  docker-compose.yml            base service definitions (all environments)
  docker-compose.dev.yml        dev overlay: hot reload, exposed debug ports
  docker-compose.prod.yml       prod overlay: pulls immutable GHCR images, no debug ports
.github/workflows/
  ci.yml                        lint, typecheck, tests, builds, Docker build, security scan
  staging.yml                   develop -> build+push GHCR images -> deploy to staging
  production.yml                main -> promote staging's image (no rebuild) -> deploy
.husky/
  pre-commit                    secret scan + lint-staged (fast, per-file)
  pre-push                      typecheck + build (still fast; full suite stays in CI)
.env.example                    every environment variable the stack uses, documented
```

## Prerequisites

- **Docker Desktop**, with WSL 2 backend enabled (Windows) or native (macOS/Linux). This is the only hard requirement for running the stack — you do **not** need PostgreSQL, Redis, Node, or the .NET SDK installed on your host, since everything containerized runs inside Docker.
- To develop **outside** the containers (e.g. for IDE debugging, faster inner loop): .NET SDK 8, Node.js 20+.
- Git.

### Windows-specific: WSL 2

Docker Desktop requires the WSL 2 backend on Windows. If `docker` commands fail with a virtualization error:

1. In an **administrator** PowerShell: `wsl --install` (or `dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart` followed by the same for `VirtualMachinePlatform`, then restart).
2. Reopen Docker Desktop — it should start cleanly.
3. Run all the commands below from a regular terminal (PowerShell, Git Bash, or a WSL Ubuntu shell all work identically — Docker Desktop exposes the same daemon to all of them).

## Getting started

```bash
git clone https://github.com/<your-org>/shopkeeper.git
cd shopkeeper
cp .env.example .env
# edit .env: at minimum set POSTGRES_PASSWORD and JWT_SECRET to real values
```

Start the full stack (Postgres, Redis, API with hot reload, frontend with hot reload):

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml --env-file .env up -d
```

- Frontend: http://localhost:5173
- API + Swagger: http://localhost:8080/swagger
- Postgres (host tools/GUI only — containers reach it as `postgres:5432`): `localhost:5433`
- Redis (host tools only): `localhost:6379`

Apply database migrations (schema doesn't create itself):

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml exec api sh -c \
  "cd /src && dotnet ef database update \
    --project src/ShopKeeper.Infrastructure/ShopKeeper.Infrastructure.csproj \
    --startup-project src/ShopKeeper.Api/ShopKeeper.Api.csproj"
```

(`cd /src` first because the container's working directory defaults to the API project folder, not the repo root the `--project`/`--startup-project` paths are written relative to.)

You should now be able to register an account, complete onboarding, and see the dashboard at http://localhost:5173.

### Running outside Docker (faster inner loop / IDE debugging)

Still use Docker for just the data layer:

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml up -d postgres redis
```

Then run the apps natively:

```bash
# backend (Swagger at http://localhost:5064/swagger)
cd backend && dotnet run --project src/ShopKeeper.Api

# frontend
cd frontend && npm install && npm run dev
```

`appsettings.Development.json` and `frontend/.env.development` are already pointed at `localhost:5433` (Postgres) and `localhost:5064` (API) respectively for this mode.

## Common commands

| Task | Command |
|---|---|
| Start dev stack | `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml --env-file .env up -d` |
| Stop dev stack | `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml down` |
| Stop and wipe all data (fresh start) | add `-v` to the above: `... down -v` |
| Rebuild images (after Dockerfile/dependency changes) | add `--build` to the `up` command, or `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml build --no-cache` |
| View logs (all services) | `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml logs -f` |
| View logs (one service) | `... logs -f api` |
| Enter a running container | `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml exec api sh` (or `frontend`, `postgres`) |
| Inspect running containers | `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml ps` |
| Inspect local images | `docker images` |
| Run a new migration | see [`database/README.md`](database/README.md) |
| Seed development data | *(not implemented yet — see `database/README.md`)* |

## Environment variables

Every variable the stack reads, and which environment needs it, is documented in
[`.env.example`](.env.example) — copy it to `.env` and fill in real values. `.env` is
git-ignored; nothing in it should ever be committed. Production/staging secrets are
never stored in a file at all — they live in GitHub Environment Secrets (see below).

## Git workflow

Git Flow, enforced by branch protection on GitHub (Settings → Branches — configure once, per repo, via the web UI; not something a script can safely do on your behalf):

```
feature/*  --PR-->  develop  --auto-deploy-->  staging  --QA-->
                                                              main  --auto-deploy--> production
hotfix/*   --PR-->  main (and back-merged into develop)
```

- **`main`** — production. Every push here (normally a merged PR from `develop`) triggers `production.yml`.
- **`develop`** — staging. Every push here triggers `staging.yml`.
- **`feature/<name>`** — normal work, branched from `develop`, PR'd back into `develop`.
- **`hotfix/<name>`** — production emergencies only, branched from `main`, PR'd into `main`, then also merged into `develop` so the fix isn't lost on the next release.

Recommended branch protection rules for both `main` and `develop`: require a pull request, require the CI checks (`frontend`, `backend`, `secret-scan`, `docker-build`) to pass, no direct pushes.

**A branch is not an environment.** `develop` deploying to staging and `main` deploying to production is a decision encoded in `staging.yml`/`production.yml`'s trigger conditions, not an inherent property of Git.

## Pre-commit / pre-push hooks

Installed automatically via `npm install` at the repo root (the `prepare` script sets up Husky). Fast, local, per-file checks only:

- **pre-commit**: secret scan (`scripts/scan-secrets.mjs`) over the staged diff, then `lint-staged` — oxlint + Prettier on changed frontend files, `dotnet format` on changed backend files. Auto-fixes what it can and re-stages the result; blocks the commit if the secret scan finds something or a fix isn't possible.
- **pre-push**: frontend TypeScript check + backend build. Still fast (seconds) — the full test suites, Docker builds, and vulnerability scan only run in CI, so pushing never takes minutes.

Nothing here is bypassable by accident — if you genuinely need to skip a hook once (e.g. committing a generated lockfile), use `git commit --no-verify` deliberately, not as a habit.

## CI/CD pipeline

```
git commit → pre-commit hook (secret scan, lint, format)
     ↓
git push → pre-push hook (typecheck, build)
     ↓
GitHub PR → ci.yml (lint, typecheck, both test suites, both builds, Docker build, Trivy scan, gitleaks)
     ↓ (merge to develop)
staging.yml → build versioned images → push to GHCR → deploy to staging
     ↓ (QA on staging, then PR develop → main)
main → production.yml → retag staging's exact image (no rebuild) → deploy to production
```

### Where images live

| Location | Purpose |
|---|---|
| Your machine's local Docker image store (`docker images`) | Whatever you've built locally — not shared, not durable. |
| GitHub Container Registry (`ghcr.io/<owner>/shopkeeper-api`, `shopkeeper-frontend`) | The single shared, versioned source of truth. Every image that reaches staging or production was pushed here by CI, never built by hand on a server. |
| Staging/production servers | Pull images from GHCR; never build from source. |

### Tagging

- `sha-<short-commit>` — every image `staging.yml` builds, immutable, always traceable back to an exact commit.
- `develop` — floating tag, always the latest staging build.
- `vX.Y.Z` or `main-<short-commit>` — what `production.yml` promotes staging's image to. Production always deploys one of these, never `develop` and never a bare `latest` alone (a `latest` tag is also pushed for convenience, but it is never what a deploy references).

### Configuring a real staging/production target

`staging.yml` and `production.yml` ship with working build/push logic but their `deploy` jobs intentionally **skip** (not fail) until you point them at a real host — there's no server to deploy to by default, and pretending otherwise would be worse than being explicit about it. To activate:

1. Provision a host with Docker installed, and this repo (or at least `docker/` and `database/`) checked out at `/opt/shopkeeper`.
2. GitHub repo → Settings → Environments → create `staging` and `production` environments.
3. Per environment, add variables `STAGING_HOST`/`PRODUCTION_HOST` (hostname/IP) and `STAGING_SSH_USER`/`PRODUCTION_SSH_USER`, and secret `STAGING_SSH_KEY`/`PRODUCTION_SSH_KEY` (a private key whose public half is authorized on that host).
4. On the host itself, create `/opt/shopkeeper/.env` with the production values from `.env.example` (`POSTGRES_PASSWORD`, `JWT_SECRET`, `IMAGE_OWNER`, etc.) — this file is never touched by CI/CD, only by whoever administers the host.

Once `STAGING_HOST`/`PRODUCTION_HOST` are set, the deploy jobs activate automatically on the next run.

## Security notes

- Containers run as a non-root user (both the API and Nginx images create and switch to an unprivileged user).
- `.dockerignore` in both `backend/` and `frontend/` keeps `.env` files, `.git`, and build artifacts out of images.
- Postgres and Redis are never published to the host in the production overlay — only reachable from other containers on `shopkeeper-network`. Only the frontend's Nginx (which reverse-proxies `/api/*` to the backend) is public.
- CI runs Trivy (container vulnerability scanning, fails on CRITICAL) and gitleaks (secret scanning across the full diff) on every push/PR, in addition to the pre-commit secret scan.

## Troubleshooting

- **`password authentication failed` connecting to Postgres from the host** — something else on your machine (often a native PostgreSQL install) is already bound to port 5432 and intercepting the connection meant for Docker. This is why the dev overlay publishes Postgres on host port **5433**, not 5432 — make sure whatever you're connecting with (a GUI tool, `psql`, `dotnet ef` run outside Docker) uses 5433, not 5432.
- **Frontend can't reach the API in the dev container** — the Vite dev server runs in the browser, not inside the Docker network, so it needs `VITE_API_BASE_URL=http://localhost:8080/api` (the host-published port), not the internal `http://api:8080`. Already set in `docker-compose.dev.yml`.
- **Docker Desktop won't start, "virtualization not detected"** — see the WSL 2 section above; this is almost always WSL 2/Virtual Machine Platform not being enabled, even when the BIOS/firmware setting is already correct.
- **Hot reload isn't picking up backend changes in the container** — confirm `DOTNET_USE_POLLING_FILE_WATCHER=1` is set (it is, in `docker-compose.dev.yml`); file-change events don't always propagate through Docker's bind-mount layer on Windows/macOS without it.
- **`dotnet format`/lint-staged seems to hang or fail in the pre-commit hook** — it needs the .NET SDK on your host (not just in Docker) to run outside a container; see Prerequisites.

## Status

**Phases 1–6 complete.** Phase 7 partially built. Phase 8 not started. See the master
build prompt for the full phase breakdown:

1. Project setup, design system, auth, onboarding, database, roles, navigation — **done**
2. Products, inventory, POS, sales, payments, receipts — **done**
3. Expenses, profitability engine, dashboard, reports, analytics — **done**
4. Branches, employees, suppliers, customers — **done**
5. Offline POS, synchronization, audit logs, notifications — **done**
6. AI Business Consultant — **done** (zero-cost, calculation-based; not LLM-backed yet)
7. Subscriptions, enterprise permissions, advanced reporting, integrations — **partial**:
   subscription plan tiers and custom roles for Enterprise are shipped; a
   period-over-period comparison view for the Profitability report is in review;
   real payment checkout (Paystack), third-party integrations, and a public API are
   not started
8. Cross-platform distribution: PWA, iOS/Android app stores, macOS/Windows desktop installers — not started
