# M2 trusted Sales authentication contract

Status: implemented and deployed to remote test, 2026-10-31. Protected cookie
fixture → real Sales SSR/circuit → live SQL entry was exercised both locally
and on the remote stand. Direct employee Portal password login itself was not
re-executed by automation. The current write scope and execution record are in
[verification](milestone-2-verification.md); the historical local-only checks
below describe their original stage. Parent plan: [milestone-2-plan.md](milestone-2-plan.md).

## Evidence and trust boundary

- Original baseline `Sales.Api/Program.cs` authorized cookies, the raw structured bearer,
  and optional DevAuth using one default policy.
- `PortalStructuredBearerToken.TryParse` trusts text fields separated by `|`;
  `PortalStructuredBearerAuthenticationHandler` checks expiry only when present.
  There is no cryptographic signature check on that transport.
- Portal `/auth/login` first calls Warehouse login, then issues the protected
  `LKvitai.MES.Portal` cookie with a GUID `NameIdentifier`, authenticated
  `Cookies` identity and `AuthenticationProperties.ExpiresUtc` from login.
- Portal/Sales hosts already share the DataProtection application name
  `LKvitai.MES.PortalAuth` and key ring. This protected cookie is the available
  trusted boundary; the unprotected `warehouse_access_token` claim is not the
  Sales credential even when it happens to be contained in that cookie.
- Sales design review uses a distinct `SalesReviewCookie`, and its selector
  prefers a successfully validated review session. Expired, tampered or
  key-rejected review cookies fall back to native Portal cookie validation.
  Never combine identities or elevate a valid selected review session.

All source paths above are within `src/Modules/Sales/` with project names
`LKvitai.MES.Modules.Sales.*`, except shared PortalAuth under `src/BuildingBlocks`
and Portal login under `src/Modules/Portal`.

## Selected approach

Use a Sales-only DataProtection credential over the existing key ring, issued
inside Sales.WebUI from a **successfully validated explicit Portal cookie**.
The API accepts only this scheme for every real Sales read/write endpoint.
Leave other modules' authentication registrations and login unchanged.

Two distinct protected payloads prevent a client supplying a user ID from
becoming an employee credential:

1. Session proof: `purpose = LKvitai.MES.Sales.PortalSession.v1`, audience
   `sales-webui`, payload version, GUID Portal subject, display name,
   cookie expiry timestamp, proof issued/expiry timestamps and random
   proof ID. Valid for at most **3 minutes**, capped by the original validated
   cookie expiry. Source is fixed `portal-cookie`, not a request field.
2. API credential: `purpose = LKvitai.MES.Sales.ApiCredential.v1`, audience
   `sales-api`, version, same subject/name/source and proof ID, issue time and
   expiry. Valid for at most **2 minutes**, capped by session-proof and cookie
   expiry. The circuit-scoped API client creates it immediately before a call.

Use `IDataProtector` for authenticated encryption plus explicit payload expiry
and lifetime validation using the injected clock. Expiry is checked on every
read, independently of the key-ring lifetime. Reject missing
expiry, bad version/audience/source, malformed or empty GUID subject,
future-issued payload beyond small configured clock tolerance (maximum 30s),
overlong lifetime, tampering, wrong key ring/purpose and expired payload. Treat
cryptographic/format errors as authentication failures, not 500 errors. Never
serialize the old bearer or accept claimed roles as proof of identity. Use
`TimeProvider` for deterministic expiry tests.

A common small codec can live in
`Sales.Infrastructure/Security/SalesCredentialProtector.cs` (both hosts
already reference Infrastructure), with a DataProtection framework reference
as needed. Registration and use remain Sales-only. There is no new auth server,
public token exchange, user database or custom cryptographic algorithm.

## Obtaining and refreshing the trusted session

At the Sales HTTP cookie-validation boundary, post-configure the named `Cookies`
options locally to Sales.WebUI, preserving any existing validation callback.
On successful validation, clone the principal and replace a private Sales
session-proof claim using the validated ticket properties. Never modify a
shared principal instance, trust a pre-existing proof claim, or produce a proof
for a review/DevAuth/anonymous/merged identity. Do not persist the proof as a new
long-lived Portal cookie claim. Reject missing/expired ticket expiry.

The framework's authenticated state flows into the Blazor circuit. A scoped
`SalesSessionCredentials` reads and unprotects the private proof from
`AuthenticationStateProvider` and checks the matching single trusted subject.
It must reject any active review claim/identity even when a valid Portal cookie
also exists. Caller-supplied DTOs/JS user IDs cannot initialize this provider.

A proof with only the captured original cookie lifetime would allow a circuit
to mint credentials after browser logout. Bound that window using a small,
Sales-only HTTP refresh path:

- `GET /sales/auth/session-proof` is read-only: it explicitly validates the
  `Cookies` scheme and current selected session. It returns a fresh protected
  session proof only for the real Portal identity; an active review selection is denied.
- Return `Cache-Control: no-store`, no cross-origin CORS and no redirects.
  Require same-origin fetch metadata when supplied. A foreign origin cannot
  read the proof; GET has no business mutation and creates no financial state.
- A background browser fetch with same-origin credentials refreshes proof every
  60 seconds and on circuit reconnect. Pass only the opaque protected proof to
  the circuit service through JS interop. Bind it to the already authenticated
  circuit subject; account switching requires a new page/circuit.
- Initial SSR can use the cookie-validation proof without JS. In an interactive
  circuit the protected refresh response is untrusted transport until its
  purpose/expiry/subject are checked. Never place API credentials, raw Portal
  cookies or the Warehouse bearer in browser storage, URLs or logs.
- On refresh failure or proof expiry, stop API calls and require Portal login /
  reload while retaining pending edits in the current circuit. Do not redirect
  away or discard the edit buffer automatically. Reconnection does not bypass
  expiry or rebind a circuit to a different user.

Ordinary cookie sliding renewal is handled by a real HTTP request. The provider
cannot extend a ticket's expiry itself. Browser logout in another tab stops
new proof refreshes; existing proof/API access expires within 3 minutes (plus
clock tolerance). This is bounded revocation, not immediate global logout.
Current Portal has no central session revocation/security-stamp store; user
revocation beyond cookie behavior requires a separate verified source and must
not be claimed here. Sales write gates can still disable commands immediately
at the server independent of credentials.

## Per-request identity and dependency-injection scope

Remove `SalesApiAuthHandler` from the factory-managed pipeline. A handler
registered through `AddHttpMessageHandler` has a factory-managed DI scope,
which is different from a Blazor circuit scope. Resolving an
`AuthenticationStateProvider` there does not establish the caller's circuit;
never cache a principal/token in that handler or HttpClient default headers.

Keep `SalesApiClient` scoped. It obtains the current circuit's proof, creates a
fresh credential, and sets the Authorization header **on each new
HttpRequestMessage** before calling a user-agnostic named HttpClient. Its
handlers do transport/logging only. Do not inject circuit identity into a
singleton, static state or pooled handler. Initial prerender and a later circuit
may have separate scopes and must both follow this same proof contract.

Suppress Authorization, proof payload and request bodies containing sensitive
input from logs. A correlation ID and trusted subject ID suffice for audit.

## API policy, authorization and request safety

Register an explicit `SalesProtectedBearer` handler and `SalesEmployee` policy
whose sole scheme is that handler. Use the policy on `/api/sales` real-data
routes, including existing list/detail/filter routes and future command outcome
lookups. Remove Portal raw bearer, DevAuth and cookie schemes from that group's
accepted schemes. A malformed credential never becomes an anonymous fallback
followed by acceptance through another scheme. Return 401/403 JSON-compatible
status codes without Portal redirect responses.

Use Authorization header `Bearer <protected Sales credential>`; DataProtection
purpose distinguishes it from the old structured bearer. API reconstructs its
principal from the protected payload only. All current real Portal employees
retain the owner's agreed all-branch read access; do not invent an unconfirmed
role-to-branch model. Writes additionally require operation capability flags
and verified Testavimas SQL ownership checks from the main plan. Neither auth
nor claimed branch IDs alone authorize a mutation. Derive audit actor from the
validated API identity, ignoring submitted actor fields.

No browser cookie is sufficient to invoke Sales API commands. The API must not
read the proof cookie/claim or expose a public mint endpoint. Because commands
require an explicit Authorization credential instead of ambient cookie auth,
they cannot be authorized by a cross-site form submitting the Portal cookie.
Retain default same-origin Blazor hub policy; do not allow wildcard credentialed
CORS. Any future cookie-authorized mutation endpoint requires antiforgery tokens
and is a deliberate contract change. The proof-refresh GET never writes orders.

Review sessions continue to route to `StubOrdersQueryService` and demo catalog
before any API request. UI checks are convenience; the API independently
rejects a review identity/token, mixed identity and the review URL key. DevAuth
may remain solely for isolated stub development endpoints; it must never
participate in the live Sales policy, even in environment Test.

## Implementation locations and release sequence

1. Add shared Sales codec/payload validation; WebUI session provider, local
   cookie validation registration, proof-refresh endpoint and background
   refresh component/JS. Keep them scoped and test both prerender and circuits.
2. Change `Services/SalesApiClient.cs` to per-request credential attachment;
   remove the `SalesApiAuthHandler` registration in WebUI `Program.cs`.
3. Add API `Security/SalesProtectedAuthentication.cs`; change
   API `Program.cs` to the explicit trusted policy for all real-data routes.
4. Verify key mounts/application name match between API/WebUI, clocks are
   synchronized and the configured internal API destination is allowlisted.
   Never send credentials following an unexpected redirect to a different host.
5. Deploy both hosts as one coordinated slice with writes still default off;
   then verify normal Portal read flows, review isolation and denied raw bearer.
   Do not deploy an intermediate configuration that signs unverified principals
   or leaves a weaker alternate route. Rollback to code accepting the old raw
   bearer reintroduces the known weakness and is not a safe auth rollback.

## Verification results and remaining checks

`dotnet test tests/Modules/Sales/LKvitai.MES.Tests.Sales.Auth/LKvitai.MES.Tests.Sales.Auth.csproj -c Release`
passes 11 tests: codec expiry/tamper/key/purpose/invalid payloads; concurrent
scoped API clients attach A/B identities correctly; expired proof sends no
request; guest sample flow sends no request; actual TestServer Portal cookie
validation → proof refresh → trusted API read; raw bearer/cookie-only denial;
valid Portal+review-cookie denial; cross-site refresh denial. The test host uses
an ephemeral key ring and synthetic isolated endpoints, never production SQL.
WebUI Release build passes without warnings. Sales CI now runs this project.

Local published-host browser verification (2026-10-31): explicit isolated API
Stub, same temporary key ring, protected Portal fixture cookie issued with the
real cookie scheme/purposes, Chromium via installed Chrome and Playwright.
SSR rendered seven rows; connected-circuit search and clearing search made
four successful API calls; JS session-proof initial/forced refresh returned
200. `New order` expanded `VV-123` to `VV-123-26` and opened the local draft
without any API mutation. Removing the browser cookie made proof refresh 401
and displayed renewal guidance while preserving the draft; restoring the same
account cookie refreshed successfully and retained the editor. The expected
401 fetch was the sole console resource error in this intentional logout case;
there were no page errors. Guest review exchange/search made zero actual API
calls, guest proof refresh was 403 and API access was 401; raw bearer was 401.
No production connection, SQL writes or production auth bypass was used.
Temporary issuer, key ring, cookies, published hosts and script were removed.

The broader browser/deployment cases below remain required before release:

- Valid Portal login → prerender list → connected circuit → trusted API read;
  expiry/sliding cookie refresh and reconnect; subject remains stable.
- Codec wrong purpose/key/audience/source/version, absent/overlong expiry,
  tampered subject, expired proof/token, malformed sizes and future timestamps.
- Raw structured bearer, plain text subject, Portal cookie alone at API, review
  key/cookie, synthetic DevAuth, review+Portal cookies and forged mixed identity
  are all denied real data/commands. A proof is not accepted as an API token.
- Two concurrent authenticated circuits A/B and parallel prerender requests:
  interleave requests repeatedly, assert actor/header/result/audit never swap.
- Account switch, logout in same/another tab, failed background refresh and
  expiry: no new credentials after proof expiry; pending edits retained; no
  silent anonymous fallback or demo fallback for a real user.
- Cross-origin form/fetch and wrong-origin hub connection cannot authorize a
  command; proof endpoint returns no credential to another origin.
- Verify HTTP no-redirect auth errors, no-store refresh, redacted logs, same key
  ring across restart/replicas, no accidental cross-module purpose acceptance.

Scope guidance follows Microsoft's documentation on
[IHttpClientFactory handler scopes](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory-troubleshooting)
and [Blazor server authentication state](https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-8.0).


Browser refresh URLs use a root-relative `/sales/auth/session-proof` path.
Reverse-proxy SSR may report its internal HTTP scheme; copying that scheme
into an absolute browser fetch URL would block HTTPS session renewal. The
relative URL keeps the browser origin and cookie boundary intact without
trusting arbitrary forwarded headers. Category sprite links use the same
root-relative approach. The command API continues to require its protected
bearer and does not accept the Portal cookie as a direct write credential.

## Bounded login diagnostics

`SalesAuthDiagnostics:ExpiresAtUtc` is default-off. A configured UTC deadline
must be in the future and no more than one hour away. During that window the
Sales WebUI logs Warning-level authentication metadata for GET `/`, GET
`/_blazor` and POST `/_blazor/negotiate` after its path base is removed. This
includes whether the Portal cookie was present, explicit `Cookies` scheme result,
allowlisted failure reason (`UnprotectTicketFailed`, `TicketExpired`, `Other`),
exception type only, context authentication state/count/type family, expected
scheme boolean, ticket expiry and nonempty-GUID subject boolean.

No cookie/ticket/proof, claim value, raw exception message, query or request body
is logged. No endpoint is added and selected context/review authentication is
unchanged. Remove the setting or let it expire to stop diagnostics. This is for
native Portal login/circuit diagnosis, not an alternative login mechanism.


## Invalid design-review cookie fallback (2026-11-01)

The previous presence-only policy selector sent every request carrying the
Sales-path review cookie to that handler, even when the review cookie was
invalid. A valid native Portal session could therefore decrypt successfully in
the explicit Cookies scheme while HttpContext.User remained anonymous, causing
an immediate Sales-to-Portal login loop.

`SalesReviewSelectorHandler` now authenticates configured review access first.
A valid review session still wins over a valid Portal session and remains limited
to sample data: session-proof refresh is 403 and cookie-only API access is 401.
If review authentication fails, existing Portal cookie authentication is tried;
no cookie-presence trust or identity merging is used. Expired or tampered Portal
cookies remain invalid. Removing/expiring review settings disables that path.

The selector uses IWebHostEnvironment, matching the Web host environment passed
to AddSalesReviewAccess; generic host environment and Web host environment can
differ in tests. Tests cover valid/expired/tampered/key-mismatched/invalid-identity
review cookies, absent cookies, disabled settings, native Portal combinations,
single-principal selection and unchanged proof/API isolation.


Native Safari verification on build `m2-0a4bb90` reused the original Portal/admin
cookie, without replacing it with a synthetic fixture. Before the fix, explicit
Cookie authentication succeeded while the default principal had zero
identities. After the fix, initial GET/negotiate/WebSocket selected one Cookies
identity, session-proof and real orders/filters returned200. New order loaded
live references and was cancelled without creation. Temporary auth diagnostic
configuration was removed; a normal Safari reload remained authenticated.
Only Sales WebUI was replaced; API and SQL observer containers/configuration
remained unchanged. Server route audit confirmed zero business mutation calls.
