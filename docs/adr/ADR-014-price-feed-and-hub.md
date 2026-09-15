# ADR-014 — Price feed parameters and hub delivery

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-014 | The synthetic feed is a symmetric uniform random walk: each second every active instrument moves by a random fraction in [−0.2%, +0.2%] of its price, snapped to its tick size and never below one tick. Parameters live in the `PriceFeed` section (`Enabled`, `IntervalSeconds`, `Volatility`); the generator sits behind `IPriceGenerator` and takes a seedable `Random`. One tick is a scoped class, `PriceTick`, that the hosted `PriceFeedService` runs in a fresh scope every interval and that tests run by hand with the feed disabled. Features push through `IPositionNotifier`; the only SignalR-aware code is in `Hubs`. `PositionValued` is sent per open position per tick to the group `account-{id}`; `PositionUpdated` is sent by the capture handler after its commit. The hub enforces the same `AccountAccess.Decide` rule as REST and reports refusals as `HubException`, whose message reaches the client. | Design section 13 asks only for "a random walk with per-instrument volatility"; a uniform step is enough to make numbers move visibly and is a one-line explanation. Snapping to the tick grid keeps prices looking like prices, and the one-tick floor means the walk cannot produce zero or negative prices, which `PositionMath` would reject. Separating the tick from the loop is what makes it testable without waiting for timers, and the feed must be off in tests anyway or the seeded prices the valuation tests rely on would drift under them. Putting the notifier behind an interface keeps `Features` free of SignalR, matching the dependency rules in design section 9. The hub check exists because a subscription is a read: a client that could not `GET` an account's positions must not receive them pushed. |

Consequences: volatility is uniform across instruments; per-symbol variation
is a small extension of `PriceFeedOptions`. An instrument created without a
starting price is not quoted until one exists. Sending to a group with no
subscribers is free in the in-memory SignalR backplane, so the feed does not
track who is listening. The hub is constructed per invocation and creates
its DbContext from `IDbContextFactory`, which required the DbContext options
to become singletons; the scoped `TradeBookDbContext` handlers use is
unchanged.
