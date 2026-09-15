# ADR-012 — Query endpoint conventions

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-012 | The blotter pages with `page` (1-based) and `pageSize` (default 50, maximum 200) and returns `{ items, page, pageSize, totalCount }`. Date filters form a half-open interval: `fromUtc` inclusive, `toUtc` exclusive. Ordering is execution time descending with trade id descending as the tie-breaker. The positions endpoint hides flat positions unless `includeFlat=true`, orders by symbol, and returns null price fields for an instrument that has no price yet rather than omitting the row. Valuation is computed in memory with `PositionMath.UnrealisedPnl`, not in SQL. | The page and limits are what design section 7 specifies; `totalCount` lets the page render "1–50 of 312" without a second call. Half-open intervals let a client walk consecutive windows without seeing a boundary trade twice. Two executions can share a timestamp, so without the id tie-breaker SQL Server is free to return them in either order and a trade could appear on two pages or neither. Hiding flat positions is the suggestion in design section 13; showing them is one flag away for the "TSLA 0 / realised 80.00" case the prototype displays. Keeping the unrealised formula in `PositionMath` means there is one implementation of it for the REST response, the hub message and the tests. |

Consequences: unknown account ids are 404 problem details, as for trade
capture. Neither endpoint applies an ownership check yet; build step 7 adds
it at the marked line in each handler. The blotter's two queries (count and
page) run without a transaction, so a trade booked between them can make
`totalCount` off by one for that response; acceptable for a blotter.
