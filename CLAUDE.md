# CLAUDE.md — TradeBook

Read `docs/design.md` before writing any code. It is the specification. This
file covers how to work in this repository; the design document covers what to
build.

## Project

TradeBook records equity trade executions, derives positions from them using
weighted-average cost, and pushes live valuations to the browser over SignalR.
.NET 9, ASP.NET Core Web API, EF Core, SQL Server, Keycloak, Docker.

The owner is learning C# and .NET through this project. That changes how you
should work here. See "Boundaries" below.

## Boundaries — read this before generating code

Some files are written by the owner, by hand. Do not generate or rewrite them.

**Off limits:**

- `src/TradeBook.Api/Domain/PositionMath.cs`
- `src/TradeBook.Api/Domain/PositionState.cs`
- The concurrency handling in the trade capture handler: the retry loop, the
  `DbUpdateConcurrencyException` catch, and the reload

If a task requires changes in those files, stop and say what needs to change and
why. Do not edit them.

**Yours to build:**

- Project scaffolding, `.csproj` files, `Program.cs`, dependency injection setup
- EF Core entity configurations, `DbContext`, migrations, seed data
- Controllers, request and response models, validation
- The SignalR hub plumbing and the price feed hosted service
- `wwwroot/index.html`
- Dockerfile, `docker-compose.yml`, Keycloak realm export
- GitHub Actions workflow
- Test scaffolding and the integration tests

**Explaining is always wanted.** When you write something non-obvious, say why
in the response, not only in a comment. If asked how something works, answer in
full. The goal is that the owner can defend every file in an interview.

## Conventions

- Target framework `net9.0`. Nullable reference types enabled.
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- File-scoped namespaces. One type per file.
- `decimal` for every quantity, price and money value. Never `double` or
  `float`.
- All timestamps are UTC. Property and column names end in `Utc` / `_utc`.
- Database naming is `snake_case`; C# is `PascalCase`. Map explicitly in the
  entity configurations rather than relying on a global convention.
- Async all the way down. Every method that touches the database or the network
  is `async` and takes a `CancellationToken`. No `.Result`, no `.Wait()`, no
  `async void` outside event handlers.
- Controllers are thin. They validate the request shape, call a handler, and map
  the result to a status code. Business logic does not live in controllers.
- `Domain` has no `using` for EF Core, ASP.NET Core, or any package. Keep it
  plain C#. If you find yourself needing a dependency there, that logic belongs
  in `Features`.
- No repository or generic repository over `DbContext`. See ADR-004.
- No AutoMapper. Write the projections by hand.
- No MediatR unless asked. Handlers are plain classes registered as scoped
  services.

## Commands

```bash
# run the stack
docker compose up -d

# add a migration
dotnet ef migrations add <Name> --project src/TradeBook.Api

# apply migrations
dotnet ef database update --project src/TradeBook.Api

# tests
dotnet test

# unit tests only (no database needed)
dotnet test --filter "Category=Unit"
```

## Things that are easy to get wrong here

- **`DbContext` in the hub.** `PositionHub` is created per connection but
  `DbContext` is scoped. Do not inject `DbContext` into a singleton or capture
  it across the connection lifetime. Resolve a scope per hub method, or inject
  `IDbContextFactory<TradeBookDbContext>`.
- **`DbContext` in the hosted service.** `IHostedService` is a singleton.
  `PriceFeedService` must create a scope per tick with `IServiceScopeFactory`,
  not inject `DbContext` directly. This will throw at startup if you get it
  wrong.
- **SignalR token transport.** WebSocket clients cannot set an `Authorization`
  header. The JavaScript client sends the token in the `access_token` query
  string, so the JWT bearer options need an `OnMessageReceived` event that reads
  it for requests to `/hubs/*`.
- **`rowversion` mapping.** Use `.IsRowVersion()` on the `Position.RowVersion`
  property. Do not model it as a plain `byte[]` or set it manually.
- **Decimal precision.** SQL Server defaults `decimal` to `(18,2)` if the
  precision is not configured. Every money and quantity column must set its
  precision explicitly in the entity configuration, or six decimal places of
  average cost will silently disappear.
- **Trades are append-only.** Do not add an update or delete endpoint for
  trades, and do not add `Update` or `Remove` calls against the `Trades` set.
  Corrections are made by booking an offsetting trade.
- **Positions are derived.** Never write a code path that sets `net_quantity`,
  `average_cost` or `realised_pnl` from anything other than the output of
  `PositionMath`.

## Definition of done for a task

- It compiles with no warnings.
- `dotnet test` passes.
- New behaviour has a test. Domain logic gets a unit test; anything touching the
  database gets an integration test against Testcontainers.
- Any decision worth questioning is written into `docs/adr/` as a short record,
  following the format in `docs/design.md` section 12.
- The change does not touch anything in the "Off limits" list.

## Build order

Work in this sequence. Do not jump ahead; each step depends on the one before.

1. Solution, two projects, `Program.cs`, Serilog, health endpoint, Dockerfile,
   `docker-compose.yml` with SQL Server only. Prove it starts.
2. Entities, `DbContext`, entity configurations, first migration, seed data.
   Prove the schema matches `docs/design.md` section 4.
3. `Domain/PositionMath.cs` — **owner writes this**. Provide the unit test file
   covering the worked example in section 5 so it can be driven test-first.
4. Trade capture handler and `POST /api/trades`, without concurrency handling.
5. Concurrency handling — **owner writes the retry loop**. Provide the failing
   integration test that fires two simultaneous executions.
6. Query endpoints: blotter and positions.
7. Keycloak in compose, realm export, JWT bearer validation, authorisation
   policies, ownership checks.
8. `PriceFeedService` and `PositionHub`, including hub authorisation.
9. `wwwroot/index.html` with the SignalR client.
10. GitHub Actions workflow.
11. ADRs and README.
