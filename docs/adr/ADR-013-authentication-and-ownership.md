# ADR-013 — JWT validation, role claims and the ownership rule

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-013 | Tokens are validated with the standard JWT bearer handler against the realm's published keys. `MapInboundClaims` is off so `sub` keeps its name. Keycloak's realm export adds a mapper that emits realm roles as a flat `roles` claim, and the handler is told `RoleClaimType = "roles"`. `Authority` (where metadata is fetched) and `Issuer` (what tokens say) are separate settings. A fallback policy requires authentication everywhere; `/health` opts out. The ownership rule is a pure function, `AccountAccess.Decide`: a trader is refused with 403 both for accounts they do not own and for accounts that do not exist; ops sees 404 for the latter and may read and write any account. The public browser client has the password grant enabled for development only. Integration tests mint their own JWTs with a symmetric key and swap it into the bearer options; the real Keycloak flow is proven against the compose stack. | Keycloak nests realm roles under `realm_access.roles`, which ASP.NET Core cannot treat as roles without either a claims transformation on every request or a mapper in the realm. The mapper is one JSON block and keeps the API free of Keycloak-specific claim walking. Inside compose the API reaches Keycloak as `keycloak:8080` while the browser sees `localhost:8080`, so the issuer in tokens never matches the metadata URL; two settings make that explicit rather than fighting Keycloak's hostname options. Fail-closed authorisation means a forgotten attribute cannot expose an endpoint. Answering 404 to a trader for an unknown account would let anyone enumerate real account ids (design section 3, B.3). Design section 7 lists ops on `POST /api/trades`, so ops may book; the design's wording "operations may read any account" is read as the minimum, not the maximum. Running Keycloak inside every test run would add half a minute per run and test Keycloak more than TradeBook; the token *validation* code path is identical either way. The password grant lets a developer obtain a token with one `curl`, which the smoke test and the README rely on. |

Consequences: `captured_by_subject` now records the token's `sub`. The
`OnMessageReceived` hook that reads `access_token` from the query string
under `/hubs` is in place for the SignalR hub in build step 8 and is
ignored everywhere else. Passwords for the three demo users and the
Keycloak admin are in the realm export and compose file; they are
development credentials for a local demo, not secrets.
