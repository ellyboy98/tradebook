# ADR-010 — Trade capture response conventions

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-010 | `POST /api/trades` returns 201 when it books a trade and 200, with the original trade and the current position, when the external reference was already booked. Unknown account or instrument ids are 404 problem details. Every other rule failure is a 400 validation problem with each failing field named, all reported at once. There is no `Location` header. | Design section 4 says a repeat submission returns the original trade; 200 tells the caller it was a replay while keeping the body identical, so a client can treat both as success. 404 for unknown ids follows the status table in design section 7 and separates "you referenced something that does not exist" from "your numbers are wrong". Reporting all field errors together is what the UI spec expects for its per-field messages. The contract has no `GET /api/trades/{id}`, so a `Location` would point at nothing. |

Consequences: a repeat external reference with a *different* body still
returns the original. The design does not ask for a mismatch check; adding
one (409 or 422) is a possible extension. Two concurrent first submissions
of the same external reference are guarded by the unique index, not by the
handler; the loser currently surfaces as a 500 and is a candidate for the
concurrency work in build step 5. Execution time is checked strictly against
the server clock with no tolerance for skew, as the design states it.
