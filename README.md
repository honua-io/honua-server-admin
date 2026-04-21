# Honua Server Admin

Web-based administration interface for Honua Server. Built with Blazor WebAssembly and MudBlazor.

## Overview

This is the official admin UI for managing Honua Server instances:

- **OpenRosa Form Designer**: Create and manage data collection forms
- **Layer Management**: Configure feature layers and spatial schemas
- **Service Administration**: Manage map services and API endpoints
- **Analytics Dashboard**: Monitor usage and performance
- **Operator Spec Workspace**: Stub-backed three-pane NL + DSL + preview workspace for walking the spec workflow end to end

## Architecture

- **Frontend**: Blazor WebAssembly with MudBlazor components
- **Backend Communication**: Uses [honua-sdk-dotnet](https://github.com/honua-io/honua-sdk-dotnet) for gRPC client
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
