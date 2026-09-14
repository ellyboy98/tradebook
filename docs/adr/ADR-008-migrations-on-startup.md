# ADR-008 — Migrations run at API startup behind a configuration flag

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-008 | The API applies pending EF Core migrations when it starts, but only when `Database:MigrateOnStartup` is `true`. The compose stack and local development turn it on. Any environment where a pipeline owns the database leaves it off and runs `dotnet ef database update` as a deployment step. | Design section 11 requires a clean `docker compose up` to produce a working demo, and forbids applying migrations by hand against a shared database. A startup step satisfies both. Making it opt-in keeps the failure mode of two replicas racing to migrate out of any deployment that has replicas. |

Consequences: the integration test fixture migrates once and switches the flag
off for every in-process API it starts, so the fixture owns the schema and
factories never race it. The `/health` endpoint reports unhealthy until the
database exists, which is the correct signal during first start.
