# Warehouse Management System

A full-stack warehouse management system covering the complete inbound → storage → outbound lifecycle: product and location master data, lot/expiry tracking, multi-dimensional location capacity, **planned putaway and picking**, an immutable stock-movement ledger, and an analytics dashboard.

The backend is an ASP.NET Core (.NET 10) Web API built with a clean, layered architecture; the frontend is a Vue 3 single-page app. Data is stored in PostgreSQL.

---

## Features

### Master data
- **Products** — SKU, description, category, required temperature zone, unit weight/volume (used to compute location load).
- **Product categories** — hierarchical tree (parent/child), browsable and reorderable.
- **Locations** — code + structured address, type (`Storage` / `Quarantine` / `Returns`), temperature zone (`Ambient` / `Chilled` / `Frozen`), **multi-dimensional capacity** (Units / Weight / Volume — each independently capped, most restrictive wins), mixed-SKU / mixed-lot rules, active & blocked states, and per-location **preferred products**.
- **Lots** — lot number and expiry date for batch/expiry tracking.

### Inventory
- On-hand / reserved / available quantities tracked per **location + product + lot**.
- Manual inventory adjustments (with audit trail) and availability lookups.

### Inbound — Stock-In (receiving & putaway)
- Lifecycle: `Draft → Putaway → Completed` (cancellable from Draft or Putaway).
- The system **plans putaway placements automatically** via a pluggable putaway planner. Strategies: `PreferredLocation`, `ConsolidateSameLot`, `ConsolidateSameSku`, `Proximity`, `NearestEmpty`, `NearestAvailable`.
- Manual override or re-plan of placements while in Draft.
- Capacity is **reserved** when putaway starts (row-locked) so concurrent receipts don't oversubscribe a location.
- Item-by-item putaway confirmation; cancellation releases reservations and reverses already-placed stock.

### Outbound — Stock-Out (picking)
- Lifecycle: `Draft → Picking → Completed` (cancellable).
- The system **plans pick allocations** across locations/lots via a pluggable picking planner. Strategies: `FEFO` (first-expired), `FIFO`, `LIFO`, `LeastQuantity`, plus `Manual` override.
- Edit pick locations / re-plan in Draft; item-by-item pick confirmation; cancellation returns reserved stock.

### Stock transfers & movement ledger
- Move stock between locations.
- Every quantity change (stock-in, stock-out, transfer, adjustment, and their cancellations) writes an **immutable `StockMovement` record**, produced as a side effect via domain events — a complete, append-only audit trail.

### Dashboard & admin
- Dashboard with overview, inbound, outbound, inventory and capacity tabs (charts via Chart.js / ApexCharts).
- **Authentication & roles**: JWT login with three roles — `Admin`, `Manager`, `Worker`. All endpoints require authentication by default; write operations require Admin/Manager.
- **Admin panel**: user management (create / list / delete users, change passwords) and database tools (seed demo data / truncate).

---

## Tech stack

### Backend (`Api.ASP`)
| Concern | Technology |
|---|---|
| Runtime / framework | .NET 10, ASP.NET Core Web API |
| Persistence | Entity Framework Core 10 + Npgsql (PostgreSQL) |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Validation | FluentValidation |
| DI / decorators | Scrutor (assembly scanning + decorator registration) |
| Logging | Serilog (console + rolling compact-JSON file) |
| API docs | OpenAPI + Swagger UI (Swashbuckle) |
| Testing | xUnit v3, FluentAssertions, Testcontainers for PostgreSQL |

### Frontend (`Client.Vue/wms-client`)
| Concern | Technology |
|---|---|
| Framework | Vue 3 (Composition API, `<script setup>`), TypeScript |
| Build tool | Vite |
| Routing | Vue Router |
| Client state | Pinia |
| Server state / caching | TanStack Vue Query |
| UI components | PrimeVue + PrimeIcons + Tailwind CSS |
| Forms & validation | VeeValidate + Zod |
| HTTP | Axios |
| Charts | Chart.js, vue3-apexcharts |

---

## Architecture

The backend follows a **clean / layered architecture** with dependencies pointing inward. The solution (`Api.ASP/Wms.slnx`) has five source projects plus tests:

```
Wms.Api            →  Wms.Application + Wms.Infrastructure + Wms.Shared   (HTTP layer)
Wms.Infrastructure →  Wms.Application + Wms.Domain + Wms.Shared           (EF Core, Identity, dispatching)
Wms.Application    →  Wms.Domain + Wms.Shared                             (use cases / CQRS handlers)
Wms.Domain         →  Wms.Shared                                          (entities, rules, events)
Wms.Shared         →  (no project dependencies)                           (Result / Error primitives)
```

- **`Wms.Domain`** — rich domain model: aggregates (`StockIn`, `StockOut`, `Location`, `Inventory`, …) with private setters and behaviour methods that enforce invariants, value objects (`Sku`, `LocationCode`, `Quantity`, `LocationCapacity`, …), enums, domain services, and domain events. Aggregate methods return `Result` rather than throwing.
- **`Wms.Application`** — one folder per feature under `Handlers/`, each command/query co-located with its handler and validator. Also hosts the putaway/picking planners and their strategies.
- **`Wms.Infrastructure`** — EF Core `AppDbContext`, per-entity configurations, migrations, Identity, JWT token service, the domain-event dispatcher, the audit interceptor, capacity reservation service, and data seeders.
- **`Wms.Api`** — thin controllers, `Result → ProblemDetails` mapping, the global exception handler, Serilog, and OpenAPI.
- **`Wms.Shared`** — the `Result` / `Error` / `ErrorType` / `ValidationError` primitives, shared by all layers.

### Request lifecycle

```
HTTP request
   → Controller action (thin: builds a command/query, resolves the handler from DI)
   → LoggingPipelineBehavior     (Scrutor decorator — logs start/success/failure)
   → ValidationPipelineBehavior  (Scrutor decorator — runs FluentValidation)
   → Command/Query handler       (the actual use case)
   → Domain aggregates (return Result) + EF Core via IAppDbContext
   → SaveChangesAsync publishes domain events → domain-event handlers (e.g. write StockMovement)
   ← Result<T>
   ← Controller maps Result to IResult: Ok / NoContent  or  ProblemDetails
```

### API controllers
Controllers are deliberately thin. Each action injects the specific handler via `[FromServices]`, constructs a command/query, awaits `handler.Handle(...)`, and maps the outcome with a single `result.Match(...)` call. There is no business logic in the controllers. See [`StockInController`](Api.ASP/Wms.Api/Controllers/StockInController.cs) for the canonical shape:

```csharp
var result = await handler.Handle(new GetStockInQuery(id), cancellationToken);
return result.Match(Results.Ok, CustomResults.Problem);
```

### Result pattern (no exceptions for control flow)
Expected failures flow through a [`Result` / `Result<T>`](Api.ASP/Wms.Shared/Common/Result.cs) type instead of exceptions. A `Result` is either success or a single typed `Error`. `Result<T>` adds a value (only accessible on success) and implicit conversions from a value or an `Error`, which keeps handlers terse:

```csharp
if (missingProduct != default)
    return StockInErrors.ProductNotFound(missingProduct);   // Error → Result<Guid>
...
return stockIn.Id;                                          // Guid  → Result<Guid>
```

### Errors
[`Error`](Api.ASP/Wms.Shared/Common/Error.cs) carries a `Code`, `Description` and an [`ErrorType`](Api.ASP/Wms.Shared/Common/ErrorType.cs) (`Failure`, `Validation`, `Problem`, `NotFound`, `Conflict`, `Unexpected`), created through factory methods (`Error.NotFound(...)`, `Error.Conflict(...)`, …). Each aggregate has its own error catalog (e.g. `StockInErrors`, `LocationErrors`) so failure reasons are centralized and discoverable.

At the edge, [`CustomResults.Problem`](Api.ASP/Wms.Api/Infrastructure/CustomResults.cs) maps the error type to an RFC 7807 **ProblemDetails** response and HTTP status code:

| ErrorType | HTTP status |
|---|---|
| `Validation`, `Problem` | 400 Bad Request |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `Unexpected` / `Failure` | 500 Internal Server Error |

`ValidationError` aggregates per-field failures into the `errors` extension. Anything that *does* throw is caught by the [`GlobalExceptionHandler`](Api.ASP/Wms.Api/Infrastructure/GlobalExceptionHandler.cs) and returned as a generic 500 ProblemDetails (the raw exception is logged, not exposed).

### CQRS without a mediator
There is **no MediatR / mediator**. The project defines its own lightweight messaging contracts in [`Common/Messaging`](Api.ASP/Wms.Application/Common/Messaging) — `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>` and their `ICommandHandler<…>` / `IQueryHandler<…>` handlers. Controllers depend **directly** on the concrete handler interface they need; there is no dispatcher indirection or runtime handler lookup.

Handlers are discovered and registered by **Scrutor assembly scanning** (scoped lifetime) in [`AddApplication`](Api.ASP/Wms.Application/DependencyInjection.cs):

```csharp
services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
    .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)),   publicOnly: false).AsImplementedInterfaces().WithScopedLifetime()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)),  publicOnly: false).AsImplementedInterfaces().WithScopedLifetime()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false).AsImplementedInterfaces().WithScopedLifetime());
```

### Cross-cutting pipeline via Scrutor decorators
Cross-cutting concerns are implemented as **decorators** rather than MediatR pipeline behaviours. After the handlers are registered, they are wrapped with `services.Decorate(...)` (through a small `TryDecorate` helper that only decorates when a matching handler is actually registered):

- [`ValidationPipelineBehavior`](Api.ASP/Wms.Application/Common/Behaviors/ValidationPipelineBehavior.cs) — runs all FluentValidation validators for the command; on failure it short-circuits with `Result.Failure(ValidationError)` and the inner handler never executes.
- [`LoggingPipelineBehavior`](Api.ASP/Wms.Application/Common/Behaviors/LoggingPipelineBehavior.cs) — logs request start, and success or failure (pushing the `Error` into the Serilog `LogContext`).

Because logging is decorated last, it becomes the outermost wrapper, so a command flows **logging → validation → handler**. Queries get logging only.

### Domain events
Every entity derives from [`Entity`](Api.ASP/Wms.Domain/Primitives/Entity.cs), which records domain events raised inside aggregate methods (e.g. `StockIn.PutawayItem` raises `StockInItemPutawayDomainEvent`). [`AppDbContext.SaveChangesAsync`](Api.ASP/Wms.Infrastructure/Persistence/AppDbContext.cs) collects and dispatches those events **after** the save, via [`DomainEventDispatcher`](Api.ASP/Wms.Infrastructure/DomainEvents/DomainEventDispatcher.cs), which resolves the matching `IDomainEventHandler<T>` from DI. This is how inventory updates and the `StockMovement` ledger are produced as side effects of putaway, picking, transfers and adjustments — keeping aggregates focused on their own invariants. Handlers live under [`Handlers/StockMovements/Events`](Api.ASP/Wms.Application/Handlers/StockMovements/Events).

### Persistence & auditing
EF Core code-first with PostgreSQL; entity mappings live in `Persistence/Configurations` and value objects are mapped as owned types. An [`AuditInterceptor`](Api.ASP/Wms.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs) stamps `CreatedAt/By` and `UpdatedAt/By` on save, and entities support soft-delete via an `IsDeleted` flag. Migrations are applied automatically on startup, after which roles and the default admin user are seeded.

### Tests
`Wms.Tests` (xUnit v3 + FluentAssertions) contains:
- **Domain unit tests** — state machines (`StockIn` / `StockOut` transitions), capacity / `CanAccept` rules, inventory math.
- **Application tests** — command/query handlers, and the putaway/picking planners and strategies.
- **Integration tests** — run against a **real PostgreSQL** instance spun up on the fly with **Testcontainers** (so Docker must be running to execute them).

---

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS) for the Vue client
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — required to run the API/Postgres via Compose **and** to run the integration tests

### Option A — Run with Docker Compose (API + database)
From `Api.ASP/`:

```bash
docker compose up --build
```

This starts:
- **PostgreSQL 17** on `localhost:5432` (db `wms`, user `postgres`, password `qwe123`)
- **wms-api** on `http://localhost:5000` and `https://localhost:5001`

The API auto-applies migrations and seeds the default admin on startup. Swagger UI is available at `https://localhost:5001/swagger` (Development).

Then start the frontend (see [Frontend](#frontend) below).

### Option B — Run from Visual Studio
1. Open `Api.ASP/Wms.slnx` in Visual Studio (2022+ with the .NET 10 SDK).
2. You need a PostgreSQL instance matching the connection string in [`appsettings.json`](Api.ASP/Wms.Api/appsettings.json) (`Host=localhost;Port=5432;Database=wms;Username=postgres;Password=qwe123`). The quickest way is to start just the database from the compose file:
   ```bash
   docker compose up postgres
   ```
   Alternatively, set the **docker-compose** project as startup to run the API in a container, or set **Wms.Api** as startup to run it directly on the host.
3. Run **Wms.Api** (F5). Migrations and seeding run automatically; Swagger UI opens at `https://localhost:5001/swagger`.

> From the CLI you can do the same with `dotnet run --project Api.ASP/Wms.Api`.

### Frontend
From `Client.Vue/wms-client/`:

```bash
npm install
npm run dev
```

The dev server runs on `http://localhost:5173` and proxies `/api` to `https://localhost:5001`, so no extra CORS configuration is needed. (To point at a different API, set `VITE_API_BASE_URL`.) Build for production with `npm run build`.

### Default credentials
A default admin account is seeded on first run (configured under `AdminAccount` in `appsettings.json`):

| Field | Value |
|---|---|
| Email | `admin@wms.local` |
| Username | `admin` |
| Password | `Admin1234!` |

Additional users (with roles `Admin` / `Manager` / `Worker`) are created by an admin from the **Admin panel**.

### Seeding demo data
To populate the warehouse with demo products, locations, lots and stock, either:
- use the **Admin → Database** tab in the UI, or
- call the admin endpoints (`POST /api/admin/seed`, `POST /api/admin/truncate`), or
- run the API with a CLI argument:
  ```bash
  dotnet run --project Api.ASP/Wms.Api -- seed       # seed demo data
  dotnet run --project Api.ASP/Wms.Api -- truncate   # clear warehouse data
  ```

---

## Running the tests

Integration tests use Testcontainers, so **Docker must be running**. xUnit v3 test projects are executables — run the suite with:

```bash
dotnet run --project Api.ASP/Wms.Tests
```

---

## Configuration reference

Key settings (in `Api.ASP/Wms.Api/appsettings.json`, overridable via environment variables / user secrets):

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `JwtSettings:SecretKey` / `Issuer` / `Audience` / `ExpiresInMinutes` | JWT signing & validation |
| `AdminAccount:*` | Default seeded admin user |
| `Serilog:*` | Log levels and sinks (console + rolling file under `logs/`) |

> The committed secret key and passwords are development defaults — replace them for any non-local deployment (e.g. via environment variables or user secrets).
