# Honua Server Admin Feature Map

This repository owns the Blazor WebAssembly administration UI for Honua Server.

## Current Workspaces

- Core admin pages for server info, connections, connection detail/create, services, service settings, layers, layer style, layer publishing, observability, and deployment control.
- Identity pages for provider status, OIDC providers, diagnostics, and API keys.
- Operator workspaces for readiness, control center, spec workspace, data connections, license workspace, spatial SQL playground, publishing workspace, operations console, usage analytics, open data hub, app builder, print service, and annotations.
- Data connection UX for create/detail/index/diagnostics/delete confirmation, including SDK-backed clients and diagnostic mapping.
- Spatial SQL UX with schema loading, explain plans, result export, map preview, save-view flow, truncation guards, and telemetry.
- Annotation workspace with shape tools, layers, comments, moderation, saved sets, and GeoJSON/SVG/PDF export.
- License workspace with status, entitlement diagnostics, expiry bands, replace flow, and error classification.
- API coverage tooling that inventories `honua-server` endpoints and renders an admin coverage matrix.

## Source Evidence

- Page routes: `src/Honua.Admin/Pages/`
- Workspace components: `src/Honua.Admin/Components/`
- State/services/models: `src/Honua.Admin/Services/`, `src/Honua.Admin/Models/`
- API coverage audit: `tools/audit-api-surface/`, `docs/admin-ui-api-coverage/`
- Integration and component tests: `tests/`

## Server Sync Assessment

Admin is partially in sync with server control-plane capabilities. It covers connections, publishing, service settings, styles, metadata, identity, license, deployment, observability, spatial SQL, and several operator workspaces. Server capabilities still ahead of UI coverage include the full scene registry UX, rate-limit policy management, GitOps/drift/manifest approval depth, cache operations, streaming subscribers, tile operations, and some 3D/world-model paths.
