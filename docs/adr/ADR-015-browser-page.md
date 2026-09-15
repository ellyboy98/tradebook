# ADR-015 — The browser page: sign-in, data loading and what it needed from the API

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-015 | `wwwroot/index.html` is the prototype with its mock data replaced. It signs in with OpenID Connect authorization code + PKCE written out in plain JavaScript against the public `tradebook-web` client, keeps tokens in `sessionStorage`, refreshes them thirty seconds before expiry, and uses the same access token for REST and for the SignalR connection. The SignalR browser client is vendored into `wwwroot/lib/signalr`. The page learns the Keycloak URL and client id from an anonymous `GET /api/client-config`. Static files are served by middleware placed before authentication, not by `MapStaticAssets`. Four endpoints were added for the page: `GET /api/instruments`, `POST /api/instruments` and `POST /api/accounts` from the design's API table, and `GET /api/accounts` (the caller's accounts), which the table lacks. `POST /api/trades` now treats `executedAtUtc` as optional and stamps the server clock when it is absent. The page requests `includeFlat=true`. | The Keycloak JavaScript adapter is no longer served by the Keycloak server and would add a dependency to explain; the flow is under a hundred lines and every step (discovery, challenge, exchange, refresh, logout) is visible to a reader learning it. A vendored client keeps `docker compose up` working without internet access, which a CDN script would not. Hard-coding the issuer URL in HTML would break the moment the port or realm changed; the API already knows it. `MapStaticAssets` registers endpoints, and the fallback authorisation policy would then demand a token for `index.html`, which nobody has before signing in. The account rail in the UI spec cannot be drawn without a list of the caller's accounts, and traders must not be able to enumerate everyone's, so the list is filtered by the same ownership rule as everything else. A manual ticket has no execution time of its own, and a browser clock a few seconds fast would otherwise be told its trade is in the future by the strict rule the design specifies. The UI spec (section 5) wants flat positions visible with an em dash; the API's default hides them, so the page asks. |

Consequences: `GET /api/accounts` is a deliberate addition to the contract in
design section 7 and should be added to that table. Tokens in
`sessionStorage` are per tab and gone when it closes, which is the right
trade for a trading screen. The password grant on `tradebook-web` remains
enabled for the curl-based smoke tests and is not used by the page. Three
demo users exist; there is no self-registration.
