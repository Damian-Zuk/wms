# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Project-specific instructions follow in the [Project](#project-warehouse-management-system) section.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

# Project: Warehouse Management System

Full-stack WMS covering the inbound → storage → outbound lifecycle. **Backend:** ASP.NET Core (.NET 10) Web API, clean/layered architecture, EF Core 10 + PostgreSQL. **Frontend:** Vue 3 SPA (TypeScript, Vite).

`README.md` is the authoritative reference for architecture, features, and setup — **read it before non-trivial work** and keep it in sync when you change architecture, commands, or config. This section is the quick operational map and the things that bite.

## Layout

```
Api.ASP/                      backend solution (Wms.slnx); run docker compose from here
  Wms.Domain/                 entities, value objects, domain events, rules — NO outward deps
  Wms.Application/            CQRS handlers (Handlers/<Feature>/), validators, putaway/picking planners
  Wms.Infrastructure/         EF Core (AppDbContext, Persistence/Configurations, Migrations), Identity, JWT, dispatchers
  Wms.Api/                    thin controllers, Result→ProblemDetails mapping, Program.cs, appsettings.json
  Wms.Shared/                 Result / Error / ErrorType primitives (no project deps)
  Wms.Tests/                  xUnit v3 — domain unit, application, and Testcontainers integration tests
Client.Vue/wms-client/        Vue 3 SPA (src/features/<feature>/)
```

Dependency direction points inward: `Api → Application + Infrastructure`, `Infrastructure → Application + Domain`, `Application → Domain`, everything → `Shared`. Never add a dependency that points outward (e.g. Domain must not reference Infrastructure).

## Commands

Paths below are relative to the repo root (`C:\dev\repos\wms`).

| Task | Command |
|---|---|
| Build backend | `dotnet build Api.ASP/Wms.slnx` |
| **Run tests** | `dotnet run --project Api.ASP/Wms.Tests` — **NOT `dotnet test`** (see gotchas) |
| Run API | `dotnet run --project Api.ASP/Wms.Api` (needs Postgres on :5432) |
| Postgres only | `docker compose up postgres` (from `Api.ASP/`) |
| Full stack (API + DB) | `docker compose up --build` (from `Api.ASP/`) |
| Seed / clear demo data | `dotnet run --project Api.ASP/Wms.Api -- seed` \| `-- truncate` |
| Frontend dev server | `npm run dev` (from `Client.Vue/wms-client/`, proxies `/api` → https://localhost:5001) |
| Frontend build + typecheck | `npm run build` (runs `vue-tsc -b`) |
| Add EF migration | `dotnet ef migrations add <Name> --project Api.ASP/Wms.Infrastructure --startup-project Api.ASP/Wms.Infrastructure --context AppDbContext` |

Default admin (seeded on first run): `admin` / `admin@wms.local` / `Admin1234!`.

## Backend conventions

- **Result, not exceptions, for expected failures.** Aggregate methods and handlers return [`Result` / `Result<T>`](Api.ASP/Wms.Shared/Common/Result.cs). Add new failure reasons to the aggregate's error catalog (e.g. `StockInErrors`, `LocationErrors`) using `Error.NotFound/Conflict/...` factories — don't invent ad-hoc throws. Exceptions are for the truly unexpected (caught by `GlobalExceptionHandler` → 500).
- **CQRS, no mediator.** A use case = a command/query + its `ICommandHandler<>`/`IQueryHandler<>` + (optional) FluentValidation validator, all co-located under `Wms.Application/Handlers/<Feature>/`. Handlers are auto-registered by **Scrutor assembly scanning** — no manual DI registration needed. Cross-cutting validation/logging are Scrutor **decorators**, not added per handler.
- **Thin controllers.** Inject the specific handler via `[FromServices]`, build the command/query, and `return result.Match(Results.Ok, CustomResults.Problem);`. No business logic in controllers. See [`StockInController`](Api.ASP/Wms.Api/Controllers/StockInController.cs) for the canonical shape.
- **Side effects via domain events.** Inventory updates and the immutable `StockMovement` ledger are produced by domain-event handlers dispatched *after* `SaveChangesAsync`, not inline in handlers. Keep aggregates focused on their own invariants.
- **Persistence.** EF Core code-first; value objects map as **owned types**; one config class per entity in `Persistence/Configurations`. `AuditInterceptor` stamps Created/Updated; entities soft-delete via `IsDeleted`. When you change the model, add a migration (command above).

## Frontend conventions

Vue 3 `<script setup>` + TypeScript, organized by feature under `src/features/<feature>/`. **TanStack Vue Query** for server state/caching, **Pinia** for client state, **PrimeVue + Tailwind** for UI, **VeeValidate + Zod** for forms, **Axios** for HTTP. Match the patterns in the existing feature folders.

## Gotchas (these will bite)

- **`dotnet test` fails here.** `Wms.Tests` is xUnit v3 (Microsoft.Testing.Platform, `OutputType=Exe`); `dotnet test` errors with `testhost.dll not found`. Use `dotnet run --project Api.ASP/Wms.Tests`, or run the built exe directly to filter: `Api.ASP/Wms.Tests/bin/Debug/net10.0/Wms.Tests.exe -namespace "Wms.Tests.Domain.Entities"` (also `-class`, `-method`). Many `[Fact]`s live in **nested** classes — target them with the `+` form, e.g. `-class "Ns.StockInStateMachineTests+Cancel"`. The FluentAssertions commercial-license warning is noise; ignore it.
- **Integration tests need Docker running** (Testcontainers spins up a throwaway Postgres). Pure domain/application unit tests don't.
- **Persisted `DateTime`s in tests must be `DateTimeKind.Utc`.** Timestamp columns are `timestamptz`; Npgsql throws at `SaveChanges` on a `Unspecified` kind. Use `new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc)`.
- **A running dev API locks `Wms.Api/bin`,** so building `Wms.Api` fails with MSB copy errors (not compile errors). The EF migration command above uses `Wms.Infrastructure` as the startup project specifically to avoid rebuilding `Wms.Api`. To run a freshly built seeder while a container holds the old build, build to a side folder and run the `seed`/`truncate` CLI from there.
- **Migrations are version-controlled and applied on startup** (`Database.MigrateAsync()`), including in the deployed container image. Don't gitignore `Wms.Infrastructure/Migrations`, and don't auto-run `dotnet ef database update` against the dev DB — let the user apply it.
