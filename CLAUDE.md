# Engineering rules — The Shop Keeper

Practical, enforceable standards for working in this repo. Short on purpose — a rule
nobody can remember doesn't get followed. Loosely inspired by [ECC](https://github.com/affaan-m/ECC)'s
`rules/` layering (common principles + language-specific notes), consolidated into one
file because this is a two-stack app, not a multi-language tooling framework.

## Architecture

- **Clean architecture boundaries are real, not decorative.** `Domain` has zero
  external dependencies. `Application` depends only on `Domain` + abstractions
  (`Common/Interfaces`) — no EF Core, no ASP.NET Core types. `Infrastructure`
  implements those abstractions. `Api` wires DI and stays thin: controllers call
  MediatR, they don't contain business logic.
- **Every tenant-owned entity implements `ITenantEntity`** and gets an automatic
  global query filter (see `AppDbContext.OnModelCreating`). Never bypass it with
  `IgnoreQueryFilters()` outside a handful of explicitly cross-tenant, trusted code
  paths (e.g. `TokenIssuer` resolving a user's own memberships) — and when you do,
  say why in a comment, because this is the one filter that stands between two
  businesses' data.
- **Financial/inventory records are append-only ledgers, not mutable state.**
  `InventoryTransaction` records every stock change; `Sale`/`SaleItem` are never
  edited after creation, only reversed via `Refund`/void. If you're tempted to
  `UPDATE` a quantity or amount directly, you're about to break an audit trail —
  add a compensating transaction instead.
- **Snapshot financial values at the moment they're locked in.** `SaleItem.UnitPrice`
  /`UnitCost` are copied from `Product` at sale time specifically so a later price
  change never rewrites historical revenue or profit. Apply the same instinct to
  any new money-related entity.

## Git workflow

`feature/* → PR → develop → (auto-deploy) staging → QA → PR → main → (auto-deploy) production`,
`hotfix/* → PR → main`, then back-merged into `develop`. Full detail in the root
README's "Git workflow" section — don't duplicate it here, it'll drift.

- Never commit directly to `main`/`master` or `develop`.
- Commit messages: explain *why*, not just what — the diff already shows what
  changed. Mirrors this repo's existing commit history; no rigid `type: subject`
  format is enforced here (unlike ECC's convention), since descriptive prose has
  worked well so far and a fixed prefix taxonomy doesn't add much this project's size.

## Testing

- **Prefer a real (SQLite in-memory) database over mocking `IAppDbContext`.** Every
  backend test in this repo builds a genuine EF Core context against SQLite,
  because query filters, unique indexes, and cascade behavior are exactly the kind
  of thing a mock silently gets wrong. This is *how the tenant-isolation bug in
  `AppDbContext` was actually caught* — a mocked context would have hidden it.
- **No enforced coverage percentage.** ECC's common testing rule sets an 80%
  target; deliberately not adopted here. Coverage percentage doesn't verify the
  thing that actually matters for this app (tenant isolation, money math) — a
  targeted test that proves a specific invariant is worth more than incidental
  coverage from testing getters. Write tests for behavior that would be genuinely
  bad to get wrong, not to hit a number.
- **When you fix a bug found by testing, prove the test would have caught it**
  before the fix existed (revert, watch it fail, reapply). Cheap insurance against
  a test that passes for the wrong reason.
- Frontend: Vitest + Testing Library, colocated as `*.test.tsx` next to the
  component. Not aiming for exhaustive coverage — test interactive behavior users
  actually depend on (a button that's disabled while loading), not implementation
  details.

## Security

- Passwords: bcrypt only, never roll your own. Refresh tokens: stored hashed
  (SHA-256), rotated on every use, theft-reuse detected and revokes the chain.
- No secret ever goes in a committed file. `.env` is git-ignored; `.env.example`
  holds only placeholders. CI runs gitleaks on every push/PR in addition to the
  pre-commit regex-based scan (`scripts/scan-secrets.mjs`) — two different
  detection strategies, deliberately: the pre-commit one is fast and pattern-based
  for the common mistakes, gitleaks is slower but broader for CI.
- All user input goes through FluentValidation (`Application` layer commands) or
  Zod (frontend forms) before it reaches a handler — never trust a DTO.
- SQL injection isn't a category of bug here: everything goes through EF Core's
  parameterized queries. If you ever reach for raw SQL, parameterize it explicitly
  and say why EF Core's query builder wasn't enough.

## Docker / infrastructure

- `docker-compose.yml` is environment-agnostic; environment-specific values
  (build target, exposed ports, restart policy) belong in the `.dev.yml`/`.prod.yml`
  overlay, not the base file.
- Any `HEALTHCHECK`/healthcheck script that calls back into its own container
  (`curl localhost:8080/...`) must use `127.0.0.1`, not `localhost`. Alpine's musl
  resolver tries `::1` (IPv6) first; if the app only binds IPv4, `localhost`
  silently fails inside the container while the host-published port works fine —
  this exact bug shipped once already (frontend dev healthcheck) and was only
  caught by actually running the container and checking `docker inspect`'s health
  log, not by reading the Dockerfile.
- Similarly: any Nginx `proxy_pass`/`upstream` referencing another container by
  Docker Compose service name needs `resolver 127.0.0.11` (Docker's embedded DNS)
  and a variable-based `proxy_pass`, or Nginx refuses to start at all if that
  service isn't already resolvable the instant Nginx boots — not a hypothetical,
  this shipped once too.
- Never assume a Docker build "works" because it completed. Run the resulting
  container and hit it — a clean `docker build` proves the image assembled, not
  that the app inside it functions.

## When you find a bug while doing something else

Fix it, but say so explicitly (don't bury it in an unrelated commit's diff without
comment) — and where practical, prove it with a test the way the tenant-isolation
and Nginx/healthcheck fixes were: reproduce the broken behavior first, then fix,
then reverify.
