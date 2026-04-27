# Honua Server Admin

Web-based administration interface for Honua Server. Built with Blazor WebAssembly and MudBlazor.

## Overview

This is the official admin UI for managing Honua Server instances:

- **Live operator dashboard** at `/` with tiles sourced from the
  `Admin/FeatureOverviewEndpoints` family
- **Connections** at `/connections`, `/connections/new`, and
  `/connections/{id}` against `Admin/SecureConnectionEndpoints`
- **Layer publishing and style editing** at `/layers`,
  `/connections/{id}/layers`, `/connections/{id}/publish`, and
  `/layers/{id}/style`
- **Service settings** at `/services` and `/services/{name}/settings`
- **Deploy control** at `/deploy` for preflight, plan, operation submit, and
  rollback flows
- **Observability** at `/observability` for recent errors, telemetry, and
  migration status
- **Server info** at `/server-info` from
  `Admin/ConfigurationDiscoveryEndpoints`, `/admin/config`, and
  `/admin/openapi.json`
- **Operator Spec Workspace** at `/operator/spec` (stub-backed three-pane
  NL + DSL + preview, shipped in #27)
- **Identity Workspace**: OIDC provider lifecycle (list / create / edit / enable / delete), provider status, auth diagnostics, and API-key gap surface — see [Identity workspace](#identity-workspace) below
- **License Workspace**: BYOL license status, entitlement inspection, expiry banding, replace flow, and operator-actionable diagnostics — see [License workspace](#license-workspace) below
- **Spatial SQL Playground**: Browser-based PostGIS-aware SQL editor with schema autocomplete, MapLibre preview, EXPLAIN tree, and named-view save flow — see [Spatial SQL playground](#spatial-sql-playground) below
- **Data Connections Workspace**: List / create / edit / soft-disable / delete / preflight data connections, with a structured diagnostic grid and a managed-Postgres capability matrix — see [Data connections workspace](#data-connections-workspace) below

Coverage of the wider honua-server admin API is tracked in
[`docs/admin-ui-api-coverage/`](docs/admin-ui-api-coverage/) — endpoint
inventory, hand-edited coverage matrix, and a drift-guard test.
Backlog review, scope gates, and done/close hygiene are tracked in
[`docs/operating-cadence.md`](docs/operating-cadence.md).
Admin UI performance, accessibility, smoke-test, and release gate baselines
are tracked in
[`docs/admin-ui-quality-gates.md`](docs/admin-ui-quality-gates.md).
Containerized admin E2E readiness and Testcontainers configuration are tracked
in [`docs/container-e2e.md`](docs/container-e2e.md).

## Architecture

- **Frontend**: Blazor WebAssembly with MudBlazor components
- **Backend Communication**: Admin routes use the in-repo
  `IHonuaAdminClient` HTTP client with a deterministic stub fallback when
  `HonuaServer:BaseUrl` is empty. Operator S1 uses the in-repo
  `ISpecWorkspaceClient` and `ISpatialSqlClient` stubs; identity
  (`HttpIdentityAdminClient`) and data connections
  (`HttpDataConnectionClient`) call the honua-server admin REST surface
  directly through `HttpClient` + source-generated JSON. The
  [honua-sdk-dotnet](https://github.com/honua-io/honua-sdk-dotnet) gRPC client
  and the SQL HTTP adapter swap in once the matching server endpoints land
- **Deployment**: Static web app (can be hosted on CDN)

## Development

### Prerequisites

- .NET 10.0 SDK or later
- Access to Honua Server instance

### Getting Started

```bash
# Clone repository
git clone https://github.com/honua-io/honua-server-admin.git
cd honua-server-admin

# Restore dependencies
dotnet restore

# Run development server
dotnet run --project src/Honua.Admin

# Open browser to https://localhost:5001
```

### Configuration

Configure server connection in `src/Honua.Admin/wwwroot/appsettings.json`:

```json
{
  "HonuaServer": {
    "BaseUrl": "https://your-server.com",
    "HubUrl": "https://your-server.com/hubs/admin",
    "ApiKey": "your-api-key",
    "RequestTimeoutSeconds": 30
  }
}
```

`HonuaServer:BaseUrl` is the absolute URL of the Honua server. When omitted,
the admin shell falls back to `StubHonuaAdminClient` — deterministic
in-memory data so the UI is demoable before the real server is wired up.
`HonuaServer:HubUrl` is optional; when omitted, realtime admin updates use
`{BaseUrl}/hubs/admin`. If neither value is present the UI keeps its normal
request/refresh behavior and marks realtime updates as disabled.

`HonuaServer:ApiKey` is **development-only**. Blazor WebAssembly ships
configuration to the browser, so any value placed here is visible to every
client that loads the static app. `Program.cs` therefore only forwards the
key as `X-API-Key` when `builder.HostEnvironment.IsDevelopment()` is true;
production builds log a warning and refuse to attach it. Production
deployments must front the admin UI with a same-origin backend / BFF that
injects credentials server-side (or replace the dev auth scaffold with a
real OIDC bearer-token flow). Tracked as a follow-on in
[`docs/identity-admin-gaps.md`](docs/identity-admin-gaps.md).

### Operator Spec Workspace

The S1 operator workspace lives at `/operator/spec` (gated by `[Authorize]`;
a `DevAuthenticationStateProvider` stands in for the real admin auth provider
until it lands). It is stub-backed inside `honua-server-admin` so the admin
UI can demo the end-to-end spec authoring flow before the real grounding,
catalog, and apply services are wired in.

The route includes:

- A conversation pane with deterministic clarification pickers
  (`pick-dataset`, `pick-column`, `pick-value`, `specify-unit`, `specify-crs`,
  `choose-op`) and collapsible mutation diffs per turn
- A sectioned DSL editor (textarea + overlay; not Monaco) with cached `@`
  dataset and `@dataset.` column completion, hover metadata, inline
  red/yellow validation, and a canonical JSON ↔ friendly text toggle
- A preview pane that auto-routes to a plan DAG, streaming apply status with
  cache-hit badges, a sortable analysis table, a local MapLibre map preview,
  or an app-scaffold stub, based on the spec's output kind
- Click-back affordances: map-feature click inserts `@source.id=<value>`
  into the NL prompt; table-column click inserts `@source.column` into the
  DSL cursor position
- Three resizable panes with drag-splitter widths, draft spec, conversation
  history, prompt draft, and JSON-view toggle persisted to `localStorage`
  under `spec-workspace:draft:{principalId}`

#### Wiring

Backend calls funnel through `ISpecWorkspaceClient` (`src/Honua.Admin/Services/SpecWorkspace/`).
S1 ships the deterministic `StubSpecWorkspaceClient`, which loads the
embedded `Resources/spec-grammar.v1.json` and a seed catalog. The grpc-backed
implementation is tracked as a follow-on admin ticket and will be DI-swapped
without changes to the UI layer. MapLibre is vendored at
`wwwroot/lib/maplibre/` (no CDN).

The state store (`SpecWorkspaceState`) emits structured events via
`ISpecWorkspaceTelemetry` (default: `ILogger` sink) for every prompt, plan,
apply, cancel, layout change, and catalog-resolve latency. The S1 scope
deliberately excludes real grounding / catalog / apply streaming, spec
library and sharing, parameterization, scheduled runs, graphical authoring,
and mobile layouts — each is tracked as a follow-on.

### Identity workspace

The identity workspace (ticket `#22`) lives under `/admin/identity/*` on the
shared spec-editor shell. It backs onto the `honua-server` admin OIDC
endpoints (`/api/v1/admin/oidc/providers`, `/api/v1/admin/identity/providers`)
through a typed seam at `src/Honua.Admin/Services/Identity/` so a different
transport can be DI-swapped without touching the page layer.

Pages:

- `Pages/Identity/Providers.razor` (`/admin/identity/providers`) — CRUD over
  OIDC providers, with a one-click discovery test per row, masked-secret
  edit form, and a confirmation gate before destructive operations
- `Pages/Identity/Status.razor` (`/admin/identity/status`) — read-only
  catalog of identity providers reported by the server with per-provider
  reachability test buttons
- `Pages/Identity/Diagnostics.razor` (`/admin/identity/diagnostics`) —
  aggregates per-provider reachability, classifies each failure as
  *operator action* (configuration fix) versus *wait* (likely upstream
  outage), and surfaces "Pending — see follow-up ticket" cards for clock
  skew, claim-mapping coverage, and callback-URL drift (server-side gaps)
- `Pages/Identity/ApiKeys.razor` (`/admin/identity/api-keys`) — stub page
  documenting the future capability and linking to the
  [identity admin gap report](docs/identity-admin-gaps.md)

Wire contract. Every honua-server admin response is wrapped in the shared
`ApiResponse<T>` envelope (`{ success, data, message, timestamp }`); the
admin client unwraps via `Data`. DTOs live in
`src/Honua.Admin/Models/Identity/IdentityModels.cs` and mirror the server
shapes verbatim with `[JsonPropertyName]` attributes. Serialization runs
through the source-generated `IdentityAdminJsonContext` so the WASM build
stays trim/AOT-safe. `OidcProviderResponse` intentionally has no
`clientSecret` field — the server never round-trips secrets.

Plaintext OIDC client secrets are write-only and optional. `ClientSecret`
on the create form is omitted entirely (sent as `null`) when the operator
leaves it blank, mirroring `honua-server`'s nullable
`CreateOidcProviderRequest.ClientSecret` so public / PKCE-style providers
go through unchanged. When supplied, the secret lives in
`OidcProviderFormModel.ClientSecret` only for the duration of the dialog,
is sent to the server exactly once on create or rotate, and is zeroed
out from in-memory state immediately after submit. The server never
returns secrets and `OidcProviderResponse` carries no has-secret flag, so
edit dialogs render a `••••• (write-only)` placeholder — neutral about
whether the server has anything stored — and require an explicit
"Rotate secret" toggle to send a new value.

Diagnostics classify each failure as *operator action* (configuration the
operator can fix — bad authority host, wrong client credentials, missing
authority) or *wait* (likely upstream — discovery timeout, opaque 5xx).
Cards labelled "Pending — see follow-up ticket" cover server-side
capabilities the diagnostics surface promises but does not yet ship
(clock-skew detection, claim-mapping coverage, callback-URL drift); see
[`docs/identity-admin-gaps.md`](docs/identity-admin-gaps.md) for the full
list and the corresponding `honua-server` follow-ups.

The diagnostic copy mapping is centralized in
`Services/Identity/IdentityDiagnostics.cs`; the table from the design
brief drives every operator-actionable message rendered on the
diagnostics and providers pages.

The identity admin client emits structured telemetry via
`IIdentityAdminTelemetry` (default: `ILogger` sink) for every list /
create / update / delete / test call.

### License workspace

The license workspace (ticket `#23`) lives at `/operator/license` on the
shared shell as a single nav entry that opens a three-pane body —
**Status | Entitlements | Actions** — mirroring the `SpecWorkspace`
layering rather than introducing a parallel layout. The page is
`[Authorize]`-gated so the production auth provider swap-in is automatic
when `DevAuthenticationStateProvider` is replaced.

This first slice covers **BYOL only**. The display contract is shaped
to accommodate the marketplace adapters from `honua-io/honua-server#804`
without redesign — the "Issued by" cell renders `LicenseStatusDto.IssuanceSource`
and defaults to `"BYOL portal"` client-side when the server omits the
field, so AWS / Azure marketplace adapters slot in by populating the
field server-side.

Pages and panes:

- `Pages/Operator/LicenseWorkspace.razor` (`/operator/license`) — pulls
  status once on workspace open via `LicenseWorkspaceState.RefreshAsync`.
  No polling; refresh is operator-driven via the actions pane.
- `Components/LicenseWorkspace/LicenseStatusPane.razor` — current
  edition, issued-by, issued-to, expiry (local TZ + UTC tooltip), and
  the diagnostic banner.
- `Components/LicenseWorkspace/EntitlementsPane.razor` — entitlement
  rows with active/inactive state, addressable by key for "feature not
  entitled" callers.
- `Components/LicenseWorkspace/LicenseActionsPane.razor` — refresh and
  the replace-license affordance.
- `Components/LicenseWorkspace/ReplaceLicenseDialog.razor` — file
  picker + explicit confirmation gate; the upload buffer lives only in
  the dialog's local scope and is dropped on submit/dispose.
- `Components/LicenseWorkspace/ExpiryBandIndicator.razor` — reusable
  band chip driven by `ExpiryBandClassifier`.
- `Components/LicenseWorkspace/LicenseDiagnosticBanner.razor` — renders
  the diagnostic copy mapped from `LicenseDiagnosticClassifier`.

Wire contract. Server responses use the shared `ApiResponse<T>` envelope
(`{ success, data, message, timestamp }`) decoded as
`LicenseApiEnvelope<T>` via the source-generated
`LicenseWorkspaceJsonContext` so the WASM build stays trim/AOT-safe.
`HttpLicenseWorkspaceClient` is hard-pinned to the working honua-server
endpoint set:

- `GET /api/v1/admin/license` → `LicenseStatusDto`
- `GET /api/v1/admin/license/entitlements` → `IReadOnlyList<EntitlementDto>`
- `POST /api/v1/admin/license` (`application/octet-stream`) → `LicenseStatusDto`

The duplicate `LicenseAdminEndpoints` set (with a 501 `POST /license/upload`
placeholder) is intentionally avoided; consolidation is filed in the gap
report. After a successful upload the state always re-fetches
`GET /api/v1/admin/license` rather than trusting the upload response
alone, so server-side signature failures surface in the next status read
even when the upload itself returned 200.

`LicenseStatusDto` exposes `Edition`, `ExpiresAt`, `IssuedAt`,
`LicensedTo`, `IsValid`, `IssuanceSource`, `ValidationState`,
`DaysUntilExpiry`, `ExpiryWarning`, and `Entitlements`. The signed
license file content is never round-tripped to the browser: only
extracted metadata appears, and the upload byte buffer never leaves the
upload action's scope (no `IBrowserStorageService`, no logging, no DOM
text rendering).

Diagnostics. `LicenseDiagnosticClassifier` is the single source of
truth for mapping status responses (or transport failures) to the
`LicenseDiagnostic` enum: `Valid`, `Expired`, `InvalidSignature`,
`EndpointUnreachable`, `AuthenticationFailure`, `FeatureNotEntitled`,
`Unknown`. Each maps to distinct operator-action copy in
`LicenseDiagnosticCopy`. Until honua-server publishes a stable
`LicenseValidationCode` enum, the classifier pattern-matches the
free-form `ValidationState` string (substrings: `expired`, `expiry`,
`signature`, `signed`, `tamper`, `verification`); see the gap report
for the migration path. `AuthenticationFailure` is split out from
`EndpointUnreachable` because the remediation differs (401/403 → fix
credentials; 5xx/timeout → check server reachability).

Expiry banding. `ExpiryBandClassifier` always computes in UTC
(truncated to whole UTC days) and renders to local for display, so the
30 / 14 / 7 / 1-day warning bands are stable across DST boundaries.
Licenses with no `ExpiresAt` map to `Perpetual` (community edition).

Telemetry. `LicenseWorkspaceState` emits structured events via
`ILicenseWorkspaceTelemetry` (default: `LoggingLicenseWorkspaceTelemetry`
`ILogger` sink) for `status_loaded`, `status_load_failed`,
`upload_attempted`, `upload_succeeded`, `upload_failed`, and
`diagnostic_observed`. Telemetry never logs license payload bytes —
only counts, status codes, edition strings, and band/diagnostic enum
values.

DI wiring is in `Program.cs`:
`ILicenseWorkspaceTelemetry → LoggingLicenseWorkspaceTelemetry`,
`ILicenseWorkspaceClient → HttpLicenseWorkspaceClient` (typed
`AddHttpClient<>` registration sharing the identity client's
`HonuaServer:BaseUrl` resolution and Development-only `X-API-Key`
forwarding), and the scoped `LicenseWorkspaceState` store.
`StubLicenseWorkspaceClient` stays in the namespace as the
deterministic in-memory backend for direct construction in tests and
offline preview, but is not DI-registered.

Server-side gaps surfaced by this workspace are catalogued in
[`docs/license-admin-gaps.md`](docs/license-admin-gaps.md) (stable
`LicenseValidationCode` enum, discriminated upload-failure responses,
duplicate endpoint-set consolidation, server-side `IssuanceSource`,
real Ed25519 verification on `ApplyLicenseAsync`, phone-home health
field, marketplace-aware surfaces).

### Spatial SQL Playground

The SQL playground lives at `/operator/sql` (gated by `[Authorize]`; reuses
the same `DevAuthenticationStateProvider` shim as the spec workspace until
the real admin auth provider lands). It registers as a `MudNavLink` inside
the shared `Shared/NavMenu.razor` so it lives in the same shell as Spec
Workspace.

The route includes:

- A SQL editor with PostGIS-aware highlighting and schema-driven
  autocomplete (tables, columns, PostGIS function/operator reference).
  Built on the same textarea+overlay pattern as the spec workspace's
  `DslSectionEditor` — Monaco / CodeMirror is deliberately deferred to
  keep the WASM bundle flat; the inner widget can be swapped later
  without touching `SqlEditor.razor`'s public surface.
- A schema sidebar with click-to-insert tables, columns, and PostGIS
  helpers, plus a manual refresh button to defeat cache staleness.
- A results pane with `Table | Map` tabs. Map auto-selects when the result
  carries a geometry column. The geometry column is identified from the
  server-supplied `GeometryColumnIndex`, never from client guessing.
- A collapsible EXPLAIN tree with per-node row counts, actual time, and a
  warning chip when the planner row estimate is off by ≥10×. EXPLAIN
  refuses mutating SQL outright — `EXPLAIN ANALYZE` would execute the
  statement on the server and the EXPLAIN endpoint has no audited
  mutation-override hook, so the per-query override applies to Run only.
- Save-as-view dialog that returns FeatureServer / OGC API Features /
  OData URLs once the named view is registered. The dialog stays open on
  success and renders each URL with a copy-to-clipboard button so the
  operator can grab them before dismissing.
- Per-query mutation override dialog. The operator must tick the
  acknowledgement before the request is re-sent with `allowMutation=true`;
  the resulting `auditEntryId` is shown back next to the result chip.
- Result export to CSV, GeoJSON, and clipboard. Exports refuse to run
  while the result is truncated until the operator opts in via the
  toolbar.

#### Wiring

Backend calls funnel through `ISpatialSqlClient`
(`src/Honua.Admin/Services/SpatialSql/`). S1 ships the deterministic
`StubSpatialSqlClient`, which seeds two geometry tables, a curated PostGIS
reference, and an in-memory named-view registry. The HTTP-backed client
will land once the matching server endpoints
(`POST /api/v1/admin/sql/{execute,explain,views}`,
`GET /api/v1/admin/sql/schema`) ship — the page is kept behind
`[Authorize]` (the same `DevAuthenticationStateProvider` that backs the
spec workspace) until then.

`SpatialSqlPlaygroundState` emits `ISpatialSqlTelemetry` events via the
default `ILogger` sink. The vocabulary is:

| Event | Trigger |
| --- | --- |
| `schema_loaded` / `schema_load_failed` | autocomplete schema fetch outcome |
| `query_submitted` | run-query button or keybinding (with `allow_mutation`) |
| `query_completed` (latency) | run succeeded; carries `rows`, `truncated`, `has_geometry`, `audit_entry_id` |
| `query_rejected` | server / client error (mutation block, transport, server error) |
| `cap_reached` | result truncation flag; carries `row_limit` |
| `explain_completed` (latency) / `explain_rejected` | EXPLAIN call outcome |
| `view_saved` / `view_save_rejected` | named-view registration outcome |
| `mutation_override_accepted` | operator confirmed the mutation dialog |
| `export_triggered` | CSV / GeoJSON / clipboard export, with `format` and `rows` |
| `export_rejected` | client-side export failure (e.g. non-WGS84 GeoJSON) surfaced as a toolbar alert |
| `results_tab_changed` | operator toggled `Table` ↔ `Map` |

AOT/trim-friendly: all DTOs (including the `MapPreviewFeature` JS-interop
DTO) are declared in the source-generated `SpatialSqlJsonContext` ahead of
the HTTP client landing, so the upcoming `HttpSpatialSqlClient` can
serialize without reflection; the EXPLAIN parser and exporter are
reflection-free; the MapLibre interop re-uses the existing vendored
bootstrap from the spec workspace. GeoJSON export follows RFC 7946 — no
legacy `crs` member, and non-WGS84 SRIDs are rejected so reprojection
stays a server-side responsibility. The MapLibre preview enforces the
same WGS84 guard: a non-4326 result keeps the operator on the table tab
and surfaces a "preview requires WGS84" banner instead of mis-rendering
coordinates.

The S1 scope deliberately excludes Monaco / CodeMirror integration, write
SQL beyond the per-query override, multi-database routing, query history
sharing across operators, and live `pg_proc` introspection — each is
tracked as a follow-on against `honua-server` or a future admin ticket.

### Data connections workspace

The data-connections workspace (ticket `#24`) lives under
`/operator/data-connections/*` on the same shared shell as the spec and
identity workspaces. It backs onto the `honua-server` admin endpoints
under `/api/v1/admin/connections` through a typed seam at
`src/Honua.Admin/Services/DataConnections/` so a different transport
(gRPC, generated client) can be DI-swapped without touching the page
layer.

Pages:

- `Pages/Operator/DataConnections/Index.razor`
  (`/operator/data-connections`) — list view (provider, host, status,
  last-checked) with a provider-aware "New connection" menu. The health
  column normalizes case before mapping to MudBlazor chip colors. The
  list endpoint hides disabled rows (server uses `WHERE is_active =
  true`); the `disabled` chip in the row template only fires when a
  future server filter surfaces them — see gap #9.
- `Pages/Operator/DataConnections/Create.razor`
  (`/operator/data-connections/new?provider={id}`) — provider-specific
  create form, in-flight preflight test, and a confirmation gate before
  save. Stub providers (e.g., SQL Server) reach this page but cannot
  submit.
- `Pages/Operator/DataConnections/Detail.razor`
  (`/operator/data-connections/{id}`) — read / edit / soft-disable /
  delete a saved connection. Soft-disable maps to
  `PUT /api/v1/admin/connections/{id}` with `{ isActive: false }` (no
  dedicated disable endpoint); delete is gated behind a typed-name
  confirm dialog and removes audit history server-side. The edit form
  is narrower than the create form: Display name and credential mode /
  external secret reference are rendered read-only because the server's
  `UpdateSecureConnectionRequest` has no slots for them — see gap #11.
  Navigating between connection ids while editing clears the in-flight
  draft so a Save never PUTs the prior connection's body against the new
  route id.
- `Pages/Operator/DataConnections/Diagnostics.razor`
  (`/operator/data-connections/{id}/diagnostics`) — six-row preflight
  grid (`Dns → Tcp → Auth → Ssl → Capability → Version`) plus a
  managed-Postgres capability matrix.

Wire contract. Every honua-server admin response is wrapped in the
shared `ApiResponse<T>` envelope (`{ success, data, message, timestamp }`);
`HttpDataConnectionClient` unwraps via `Data` exactly like the identity
client. DTOs live in `src/Honua.Admin/Models/DataConnections/` and
mirror the server shapes verbatim with `[JsonPropertyName]` attributes.
Serialization runs through the source-generated
`DataConnectionsJsonContext` (with both raw DTOs and `ApiResponse<T>`
shapes registered) so the WASM build stays trim/AOT-safe. Every method
funnels through a single `ExecuteRequestAsync<T>` helper so network
(`HttpRequestException`), malformed-response (`JsonException`), and
cancellation / timeout (`OperationCanceledException`) failures all land
as typed `ConnectionOperationError` values rather than exceptions
escaping to Razor.

The HTTP client is registered via
`builder.Services.AddHttpClient<IDataConnectionClient, HttpDataConnectionClient>(...)`
in `Program.cs`, picking up `HonuaServer:BaseUrl` and the dev-only
`X-API-Key` header through the same pattern as the identity client.

The endpoint surface consumed by `IDataConnectionClient`:

| Method | Route                                               | Purpose                                |
| ------ | --------------------------------------------------- | -------------------------------------- |
| GET    | `/api/v1/admin/connections`                         | List summaries                          |
| GET    | `/api/v1/admin/connections/{id}`                    | Full detail                             |
| POST   | `/api/v1/admin/connections`                         | Create (returns Summary)                |
| PUT    | `/api/v1/admin/connections/{id}`                    | Edit / soft-disable / re-enable        |
| DELETE | `/api/v1/admin/connections/{id}`                    | Hard delete (typed-name confirm)        |
| POST   | `/api/v1/admin/connections/test`                    | Preflight a draft before save           |
| POST   | `/api/v1/admin/connections/{id}/test`               | Preflight a saved record                |

`DisableAsync` / `EnableAsync` on `IDataConnectionClient` are
convenience wrappers around `UpdateAsync` and do not map to dedicated
server routes. `CreateAsync` and `UpdateAsync` return
`DataConnectionSummary` — the projection honua-server returns today —
and `DataConnectionsState` issues a follow-up
`GetAsync` via `TryRefreshSelectedDetailAsync` so Detail-only fields
(`CredentialReference`, `EncryptionVersion`, `UpdatedAt`) are available
to the page after a save. The extra round-trip is tracked as gap #8 in
[`docs/data-connection-api-gaps.md`](docs/data-connection-api-gaps.md).

Server policy: `RequireAdminAuthorization`. The admin UI does not
duplicate the policy check; `401` / `403` map to a
`ConnectionOperationError(Auth)` and a banner alert. The UI's typed
error kinds are `Network`, `Auth`, `Validation`, `Server`, `Conflict`,
`NotFound`; the typed copy-keys it raises are `error.network`,
`error.timeout`, `error.malformed_response`, `error.empty_response`,
plus the kind-specific `error.{kind}` keys parsed from the response
body. honua-server returns 4xx admin failures as
`ApiResponse<object>.Failure(message)` (e.g., `"Invalid SSL mode"`,
`"Connection is in use by services"`) rather than RFC7807
`ProblemDetails`, so `HttpDataConnectionClient.ParseProblemAsync` reads
the body once and tries both shapes — `ProblemDetails.Detail`/`.Title`
first, then the failure-envelope `Message` — so the operator-actionable
message survives into the banner alert.

Diagnostic contract. Preflight always renders a six-row grid in
deterministic order (`Dns → Tcp → Auth → Ssl → Capability → Version`).
The server today returns only `{ connectionId, connectionName,
isHealthy, testedAt, message }`; `Services/DataConnections/DiagnosticMapper.cs`
is the single seam that distributes the signal across cells via a
narrow substring heuristic. Unmatched failure messages light only
`Auth`; unrelated cells stay `NotAssessed` so the UI never produces
false negatives. Pages never render the raw message as the primary
signal. Per-step diagnostic codes are tracked as gap #1 in the gap
report; the mapper becomes a pass-through once they ship.

After a successful preflight against a saved connection,
`DataConnectionsState.RunExistingPreflightAsync` patches the new
`HealthStatus` ("Healthy"/"Unhealthy") and `LastHealthCheck` into both
`SelectedDetail` and the corresponding list summary so Detail and Index
chips reflect the latest test result without a manual refresh. The
server's test endpoint does not persist the outcome to the row today
(gap #10), so this local reconciliation is the only thing keeping the UI
honest until that ships.

Managed-Postgres capability matrix. Driven by
`PostgresProviderRegistration.ManagedHostingChecks` (server version,
SSL enforced, primary-vs-replica role, PostGIS, pgaudit, Aurora IAM,
Azure AAD). Every cell renders `NotAssessed` with an
"Awaiting honua-server#644" hover until the certification endpoint
lands. The matrix carries `IsServerSourced=false` so renderers stay
honest about provenance.

Credential handling. Credentials only live in the in-memory
`ConnectionDraft` for the lifetime of one submission. The state store
(`DataConnectionsState`) clears the draft on save / cancel, never
persists it to `localStorage`, and never accepts a credential back
from the server. The detail view shows only `StorageType`
(`managed` | `external`) plus `CredentialReference` for external
secrets via `Components/Shared/MaskedSecretField` — no last-N-chars
preview today (gap #4).

Provider extensibility. `IProviderRegistration` (provider id, display
name, default port, create form, capability renderer, managed-hosting
check list) plus `IProviderRegistry` (DI-driven lookup by id) decouple
the workspace shell from provider-specific UI. Postgres is concrete;
SQL Server is the registered stub (`IsStub = true`, empty check list,
"coming soon" placeholder form). Stubs are reachable from the New-connection
menu but cannot submit. New providers ship by registering an additional
`IProviderRegistration` — no workspace shell changes required. The
load-bearing constraint is `honua-io/honua-server#362` (multi-database
epic), which must surface a real `providerId` per connection before a
second concrete provider can ship; tracked as gap #3.

Telemetry. `LoggingDataConnectionTelemetry` is the default sink for
`IDataConnectionTelemetry`. Events:
`data_connections.list_loaded` / `list_failed`,
`data_connections.create_submitted` / `create_succeeded` /
`create_failed`, `data_connections.update_succeeded` / `update_failed`,
`data_connections.test_started` / `test_completed` (`result_kind` is
`healthy | failed | not_assessed | error`, `failed_step` is set on
failures, latency in ms), `data_connections.enabled` / `disabled` /
`deleted`, `data_connections.provider_stub_viewed`.

The full set of server-side gaps surfaced while building this
workspace lives in
[`docs/data-connection-api-gaps.md`](docs/data-connection-api-gaps.md);
each entry feeds the API audit matrix tracked in
`honua-io/honua-server-admin#28`.

## Features

### Form Designer
- Visual form builder with drag-and-drop interface
- OpenRosa-compatible XML export
- Integration with server layer schemas
- Mobile preview and testing

### Layer Management
- Schema visualization and editing
- Spatial reference system configuration
- Field validation rules
- Performance monitoring

### Service Administration
- Endpoint configuration
- Authentication management
- Rate limiting and quotas
- Health monitoring
## Audit Tooling

Re-derive the API surface inventory and coverage matrix from honua-server
source (sibling `../honua-server` checkout discovered automatically;
override with `--honua-server-root` or `HONUA_SERVER_PATH`):

```bash
dotnet run --project tools/audit-api-surface -- generate
dotnet run --project tools/audit-api-surface -- seed-coverage
dotnet run --project tools/audit-api-surface -- render
```

The xunit drift guard (`Honua.Admin.Tests/Audit/CoverageDriftTests.cs`)
fails CI if the inventory and coverage rows fall out of sync, so future
audits compute the diff rather than re-doing the inventory.

## Contributing

This project follows the same contribution guidelines as [honua-server](https://github.com/honua-io/honua-server).

## License

Licensed under the Elastic License v2.0. See [LICENSE](LICENSE) for details.
