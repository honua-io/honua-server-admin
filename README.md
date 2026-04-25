# Honua Server Admin

Web-based administration interface for Honua Server. Built with Blazor WebAssembly and MudBlazor.

## Overview

This is the official admin UI for managing Honua Server instances:

- **OpenRosa Form Designer**: Create and manage data collection forms
- **Layer Management**: Configure feature layers and spatial schemas
- **Service Administration**: Manage map services and API endpoints
- **Analytics Dashboard**: Monitor usage and performance
- **Operator Spec Workspace**: Stub-backed three-pane NL + DSL + preview workspace for walking the spec workflow end to end
- **Identity Workspace**: OIDC provider lifecycle (list / create / edit / enable / delete), provider status, auth diagnostics, and API-key gap surface — see [Identity workspace](#identity-workspace) below

## Architecture

- **Frontend**: Blazor WebAssembly with MudBlazor components
- **Backend Communication**: Operator S1 uses the in-repo `ISpecWorkspaceClient` stub; the [honua-sdk-dotnet](https://github.com/honua-io/honua-sdk-dotnet) gRPC client swap is a follow-on
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

Configure server connection in `src/Honua.Admin/appsettings.json`:

```json
{
  "HonuaServer": {
    "BaseUrl": "https://your-server.com",
    "ApiKey": "your-api-key"
  }
}
```

`HonuaServer:BaseUrl` is the absolute URL of the Honua server. When omitted,
the admin UI falls back to its own host base address (assumes same-origin
deployment). `HonuaServer:ApiKey` is forwarded as `X-API-Key` on every admin
API request — this is the same authentication scheme honua-server's
`ApiKeyAuthenticationHandler` expects.

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
returns secrets; edit dialogs render a `••••• (set)` placeholder and
require an explicit "Rotate secret" toggle to send a new value.

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

## Contributing

This project follows the same contribution guidelines as [honua-server](https://github.com/honua-io/honua-server).

## License

Licensed under the Elastic License v2.0. See [LICENSE](LICENSE) for details.
