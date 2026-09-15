# ADR-011 — EF Core interceptors are attached from DI so tests can control timing

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-011 | `AddTradeBookPersistence` attaches every `IInterceptor` registered in the service collection to the `DbContext`. Production registers none. The integration tests register a `DbCommandInterceptor` that runs a callback each time the API finishes reading a position. | The concurrency requirement (design section 6) is "two executions arrive at once". Left to real timing, two HTTP requests overlap in the few milliseconds between reading the position and saving it only sometimes, so a test built on that would pass or fail at random, both before and after the retry loop exists. Holding each request at the read until the other has also read makes the conflict certain. The alternative, sleeping or firing many requests and hoping, is slower and still not deterministic. |

Consequences: one line of production code exists for testability and is a
no-op outside tests. The interceptor matches SQL text (`SELECT … FROM
[positions]`), so a change to the entity's table name or to how EF Core
shapes that query would need the test helper updated; the tests would fail
loudly rather than silently pass, because the barrier times out. The same
hook can drive the 409 path by changing the row from a second connection
after every read, which is how the "gives up after three attempts" test works.
