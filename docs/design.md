# TradeBook — Design Document

Version 0.1 · 15 September 2026

## 1. What this is

TradeBook records equity trade executions, derives the resulting positions, and
values those positions continuously against a moving price. A trader submits an
execution over REST and sees the updated position and profit or loss appear on
screen without refreshing the page.

The application is a learning and demonstration build. It is not intended for
production trading.

### Scope for version 1

In scope:

- Equity instruments only, single currency, no FX conversion
- Long and short positions, including a trade that crosses through zero
- Weighted-average-cost accounting for realised and unrealised profit and loss
- Authentication and authorisation through Keycloak
- A synthetic price feed running inside the application
- One browser page showing the blotter and live positions

Out of scope for version 1, and deliberately so:

- Multi-asset support. The data model reserves `instrument_type` so this can be
  added later without a rewrite.
- FIFO or lot-level cost accounting
- Settlement, corporate actions, fees, commissions, taxes
- Real market data
- Order management. TradeBook records executions, it does not route orders.
- Risk limits and margin

## 2. Readers and actors

| Actor | What they do | How they authenticate |
|---|---|---|
| Trader | Submits executions for accounts they own; watches live positions | Keycloak, realm role `trader` |
| Operations | Reads the blotter and positions for any account; seeds instruments and accounts | Keycloak, realm role `ops` |
| Price feed | Internal background service; no external identity | Runs in-process |

A trader may only read and write accounts where `accounts.owner_subject` matches
the `sub` claim on their token. Operations may read any account. Nobody may
amend or delete a trade once captured.

## 3. Functional flow

![Functional flow](../diagrams/functional-flow.png)

The flow has three parts.

**A. Sign-on.** The browser page has no credentials of its own. It sends the
trader to Keycloak, which authenticates them and returns a JWT access token. The
page holds that token and uses it for both REST calls and the SignalR
connection. TradeBook never sees a password.

**B. Trade capture, the write path.** This is the only path that changes state,
and every step exists to keep the position consistent with the trade history.

1. The trader posts an execution to `POST /api/trades`.
2. The API validates the token signature, issuer and audience against Keycloak's
   published keys.
3. The API checks that the caller owns the target account. A caller who does not
   gets 403, and the response says nothing about whether the account exists.
4. The API validates the payload: the instrument must exist and be active, the
   quantity must be greater than zero, the price must be greater than zero, and
   the execution timestamp must not be in the future. Failures return 400 with
   the specific field named.
5. The API opens a transaction and loads the position row for the account and
   instrument pair, including its `row_version` token. A position row is created
   on first trade.
6. The API applies the weighted-average-cost rules in section 5 to produce the
   new net quantity, average cost and realised profit or loss.
7. The trade row and the updated position row are written in one commit. The
   trade is an insert and is never updated afterwards.
8. If the commit fails because another request changed the position first, the
   API reloads the position and retries, up to three attempts. After three it
   returns 409 and the caller may resubmit.
9. On success the API publishes the updated position to the SignalR group for
   that account, then returns 201 with the trade and the position.

**C. Continuous revaluation, the push path.** A hosted service wakes every
second, generates a new price for each active instrument, stores the snapshot,
loads every position with a non-zero net quantity, computes unrealised profit or
loss, and pushes the valuations to the SignalR groups that are subscribed. The
browser updates the numbers in place.

Both paths deliver to the same hub connection, so the page has one update
mechanism rather than two.

## 4. Data model

![Entity relationship diagram](../diagrams/erd.png)

Five tables. The design decision that shapes all of them: **trades are the
record of fact and positions are derived from them.** A position row is a cached
projection that exists for query speed. If it is ever doubted, it can be rebuilt
by replaying the trades in execution order. Nothing in the application updates a
position without a trade to justify it.

| Table | Purpose | Notes |
|---|---|---|
| `accounts` | Trading accounts | `owner_subject` holds the Keycloak `sub` claim and drives authorisation |
| `instruments` | Tradeable securities | `instrument_type` reserved for multi-asset; always `1` (equity) in version 1 |
| `trades` | Executions, append-only | Insert only. No update or delete path exists in the application. |
| `positions` | Derived holdings | Unique on `(account_id, instrument_id)`. `row_version` carries optimistic concurrency. |
| `instrument_prices` | Latest price per instrument | Snapshot only, one row per instrument, overwritten each tick |

Field-level notes that matter:

- `trades.quantity` is always positive. Direction lives in `trades.side`. This
  keeps the stored data readable and puts the sign conversion in one place in
  code.
- `positions.net_quantity` is signed. Positive is long, negative is short, zero
  is flat.
- `positions.average_cost` is the weighted average cost of the currently open
  quantity. It is zero when the position is flat.
- `positions.realised_pnl` accumulates across the life of the position and is
  never reset.
- `trades.external_ref` is nullable and unique. A caller that supplies it gets
  idempotent capture: a repeat submission with the same reference returns the
  original trade rather than creating a second one.
- All money and quantity columns are `decimal`, never `float`. All timestamps
  are UTC, stored as `datetime2(3)`.

`instrument_prices` is kept in the database rather than memory only so that
positions still value correctly immediately after a restart.

## 5. Position arithmetic

This is the part of the system with real business rules, and the part most
likely to be got wrong. It must live in a pure class with no database or web
dependencies so it can be tested directly.

### Notation

For an incoming trade, let `q` be the signed quantity (positive for buy,
negative for sell) and `p` the execution price. The position state before the
trade is net quantity `N`, average cost `A` and realised profit `R`.

### Rules

**Opening from flat** (`N = 0`)

    N' = q
    A' = p
    R' = R

**Increasing an existing position** (`N` and `q` have the same sign)

    N' = N + q
    A' = (|N| × A + |q| × p) ÷ |N'|
    R' = R

**Reducing without crossing zero** (opposite signs, `|q| ≤ |N|`)

    closed  = |q|
    R'      = R + closed × (p − A) × sign(N)
    N'      = N + q
    A'      = A, or 0 when N' = 0

**Crossing zero** (opposite signs, `|q| > |N|`)

    closed  = |N|
    R'      = R + closed × (p − A) × sign(N)
    N'      = N + q
    A'      = p

The last rule is the one that separates a correct implementation from a naive
one. When a position flips from long to short, the old average cost must be
discarded and replaced with the price of the trade that caused the flip. It must
not be blended with the closed side.

### Unrealised profit or loss

    unrealised = (last_price − A) × N

The sign works out for both directions. A short position has negative `N`, so a
falling price produces a positive number.

### Worked example — run this as the test suite

Starting flat on one account and instrument.

| # | Trade | `N` after | `A` after | Realised after |
|---|---|---|---|---|
| 1 | Buy 300 @ 10.00 | +300 | 10.00 | 0.00 |
| 2 | Buy 200 @ 12.00 | +500 | 10.80 | 0.00 |
| 3 | Sell 100 @ 13.00 | +400 | 10.80 | 220.00 |
| 4 | Sell 600 @ 9.00 | −200 | 9.00 | −500.00 |
| 5 | Buy 50 @ 8.00 | −150 | 9.00 | −450.00 |
| 6 | Buy 150 @ 9.50 | 0 | 0.00 | −525.00 |

How each line arrives:

1. Opening from flat.
2. `(300 × 10.00 + 200 × 12.00) ÷ 500 = 10.80`.
3. Reducing. `100 × (13.00 − 10.80) × (+1) = 220.00`.
4. Crossing zero. Closes 400 at a loss of `400 × (9.00 − 10.80) = −720.00`,
   taking realised to `220.00 − 720.00 = −500.00`. The remaining 200 opens a
   short at 9.00.
5. Reducing a short. `50 × (8.00 − 9.00) × (−1) = +50.00`.
6. Full close. `150 × (9.50 − 9.00) × (−1) = −75.00`. Net quantity reaches zero
   so average cost resets.

### Rounding

Average cost and realised profit are held as `decimal(18,6)` and rounded to six
decimal places using banker's rounding after each trade. Rounding at each step
rather than at read time keeps the stored value and any recomputation in
agreement.

## 6. Concurrency

Two executions arriving at once for the same account and instrument would
corrupt the position under a plain read-modify-write. The system handles this
with optimistic concurrency:

- `positions.row_version` is a SQL Server `rowversion` column, mapped in EF Core
  with `.IsRowVersion()`.
- The update statement carries the original `row_version` in its `WHERE` clause.
  If another transaction has already changed the row, zero rows are affected and
  EF Core raises `DbUpdateConcurrencyException`.
- The handler catches it, reloads the position, reapplies the arithmetic from
  the new state, and retries. Three attempts, then 409.

Retry is safe because the arithmetic is a pure function of the prior state and
the incoming trade. The trade insert and the position update share one
transaction, so a failed attempt leaves nothing behind.

Pessimistic locking was considered and rejected. Contention on a single account
and instrument pair is low, and holding a lock across the arithmetic would
serialise unrelated accounts for no benefit.

## 7. API contract

All endpoints require a valid bearer token. All responses are JSON. Errors use
`ProblemDetails` (RFC 7807).

| Method | Path | Role | Purpose |
|---|---|---|---|
| POST | `/api/trades` | trader, ops | Capture an execution |
| GET | `/api/trades` | trader, ops | Blotter, filtered by account and date range |
| GET | `/api/accounts/{id}/positions` | trader, ops | Current positions with valuation |
| GET | `/api/instruments` | trader, ops | Active instruments |
| POST | `/api/instruments` | ops | Create an instrument |
| POST | `/api/accounts` | ops | Create an account |
| GET | `/health` | anonymous | Liveness and readiness |

`POST /api/trades` request:

```json
{
  "accountId": 1,
  "instrumentId": 7,
  "side": "Buy",
  "quantity": 300,
  "price": 10.00,
  "executedAtUtc": "2026-09-15T02:31:00Z",
  "externalRef": "OMS-88213"
}
```

Response 201:

```json
{
  "trade": { "id": 4412, "accountId": 1, "instrumentId": 7, "side": "Buy",
             "quantity": 300, "price": 10.00,
             "executedAtUtc": "2026-09-15T02:31:00Z" },
  "position": { "accountId": 1, "instrumentId": 7, "netQuantity": 300,
                "averageCost": 10.00, "realisedPnl": 0.00 }
}
```

Status codes: 201 created, 400 validation failure, 401 missing or invalid token,
403 account not owned by caller, 404 unknown account or instrument, 409
concurrency retries exhausted.

`GET /api/trades` takes `accountId` (required), `instrumentId`, `fromUtc`,
`toUtc`, `page`, `pageSize` (default 50, maximum 200). Results are ordered by
`executed_at_utc` descending.

## 8. SignalR contract

Hub path `/hubs/positions`. The connection carries the same bearer token as the
REST calls, passed via `access_token` in the query string, which is how the
SignalR JavaScript client sends it for WebSocket transports.

Client to server:

| Method | Argument | Effect |
|---|---|---|
| `SubscribeAccount` | `accountId` | Joins group `account-{id}` after the same ownership check the REST endpoints apply |
| `UnsubscribeAccount` | `accountId` | Leaves the group |

Server to client:

| Method | Payload | Sent when |
|---|---|---|
| `PositionUpdated` | Position with net quantity, average cost, realised profit | A trade changes the position |
| `PositionValued` | Position identifier, last price, unrealised profit, timestamp | Each price tick, for open positions only |

Authorisation is enforced in the hub, not only at the REST layer. A client that
calls `SubscribeAccount` for an account it does not own is rejected.

## 9. Application architecture

### Projects

Two, and no more until something forces a third.

    TradeBook.Api      the application
    TradeBook.Tests    unit and integration tests

A four-project Clean Architecture split was considered and rejected. On five
tables it adds indirection without decoupling anything real. The decision is
recorded as ADR-003 so the reasoning is visible rather than assumed.

### Folder layout inside `TradeBook.Api`

    Domain/                 pure business logic, no EF Core, no ASP.NET
      PositionMath.cs       the rules in section 5
      PositionState.cs      immutable record: net quantity, average cost, realised
      TradeSide.cs
    Features/
      TradeCapture/         controller, request and response models, handler
      Positions/            query endpoint and projections
      Instruments/
      Accounts/
      PriceFeed/            hosted service, price generator
    Hubs/
      PositionHub.cs
    Persistence/
      TradeBookDbContext.cs
      Configurations/       one IEntityTypeConfiguration per entity
      Migrations/
    Infrastructure/
      Auth/                 JWT bearer setup, authorisation policies
      Health/
    wwwroot/
      index.html            the single blotter page

Features are organised as vertical slices. Everything a feature needs sits in
one folder, so a change to trade capture touches one place. The exception is
`Domain`, which is shared and deliberately dependency-free.

### Dependency rules

- `Domain` references nothing. It is plain C# and must stay that way.
- `Features` may reference `Domain` and `Persistence`.
- `Persistence` may reference `Domain`. It must not reference `Features`.
- No repository layer over `DbContext`. `DbContext` already provides a unit of
  work and a repository, and wrapping it adds a layer that only forwards calls.

### Technology

| Concern | Choice |
|---|---|
| Runtime | .NET 9, C# 13 |
| Web | ASP.NET Core Web API, controller-based |
| Data | EF Core 9, code-first migrations |
| Database | SQL Server 2022 |
| Real time | SignalR |
| Identity | Keycloak 26, JWT bearer validation |
| Logging | Serilog, structured, JSON to console |
| Tests | xUnit, FluentAssertions, Testcontainers for SQL Server |
| Container | Docker, Docker Compose |
| Pipeline | GitHub Actions |

Nullable reference types are enabled and warnings are treated as errors.

## 10. Testing strategy

| Layer | Tool | What it covers |
|---|---|---|
| Domain | xUnit, no database | Every line of the section 5 worked example, plus opening from flat, exact close to zero, and crossing zero in both directions |
| Persistence and API | Testcontainers, real SQL Server | Trade capture end to end, idempotency by `external_ref`, authorisation rejection, concurrency conflict and retry |
| Hub | Integration | Subscription authorisation, message delivery |

Controllers are not unit tested in isolation. Testing a controller with a mocked
handler mostly tests the framework.

The concurrency test is worth building properly: fire two simultaneous
executions against the same position and assert that both are recorded and the
final net quantity equals the sum. Without it, the optimistic concurrency code
is an unverified claim.

## 11. Deployment

`docker-compose.yml` brings up three services:

| Service | Image | Notes |
|---|---|---|
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | Named volume for data |
| `keycloak` | `quay.io/keycloak/keycloak:26` | Dev mode, realm imported from `keycloak/realm-export.json` |
| `api` | Built from `src/TradeBook.Api/Dockerfile` | Depends on both, waits for health |

Migrations are applied by the pipeline or by an explicit startup step, never by
hand against a shared database. Seed data (a handful of instruments, two
accounts, two users) is imported with the Keycloak realm and an EF Core seeding
migration so a clean `docker compose up` produces a working demo.

The GitHub Actions workflow restores, builds, runs the unit tests, runs the
integration tests against a service container, and builds the API image.

## 12. Architecture decision records

Keep these short. They are the part a reviewer reads first.

| ID | Decision | Reason |
|---|---|---|
| ADR-001 | Trades are append-only; positions are derived | The position can always be justified from the trade history, which is what an audit needs. Nothing can edit a holding directly. |
| ADR-002 | Optimistic concurrency with `rowversion`, not locking | Contention per account and instrument is low; locking would serialise unrelated work and hold locks across business logic. |
| ADR-003 | Two projects and vertical slices, not layered Clean Architecture | Five tables do not justify four assemblies. Revisit when a second consumer or a background worker needs the domain. |
| ADR-004 | No repository abstraction over EF Core | `DbContext` is already a unit of work and a repository. A wrapper would only forward calls and complicate testing against a real database. |
| ADR-005 | Keycloak as an external identity provider | Authentication belongs outside the application. It also gives a real system boundary to document and a standards-based token flow to implement. |
| ADR-006 | Weighted average cost, not FIFO | One number per position instead of a lot ledger. FIFO is the obvious version 2 extension and the interface would not change. |
| ADR-007 | Price feed is synthetic and in-process | Real market data brings licensing and cost for no learning benefit. The generator sits behind an interface so a real feed can replace it. |

## 13. Open items

- Price generation model. A random walk with per-instrument volatility is
  enough. Decide the exact parameters when the service is written.
- Whether `GET /api/accounts/{id}/positions` returns flat positions or hides
  them. Suggest hiding by default with an `includeFlat` flag.
- Keycloak realm name and client identifiers. Fix these before the IFA is
  finalised, since both sides reference them.
