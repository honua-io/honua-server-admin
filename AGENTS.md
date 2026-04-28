# Repository Guidance

This repo owns the Blazor/MudBlazor operator UI for Honua Server. Stable admin
REST clients and shared DTO contracts should live in `honua-sdk-dotnet`, usually
under `Honua.Sdk.Admin` or a dedicated admin contracts package.

## Belongs Here

- Blazor pages, MudBlazor components, operator workspace layout, navigation,
  dialogs, page state, view models, and UI-specific validation/presentation.
- Local demo stubs and sample data used to run the admin UI without a configured
  server.
- Admin workflow composition for dashboards, publishing, deploy control,
  observability, identity, license, spatial SQL, print/export, annotations, and
  settings.
- Browser-specific UI integration and same-origin/BFF assumptions.

## Does Not Belong Here

- Stable admin REST DTOs, API envelopes, source-generated JSON contexts, or
  reusable HTTP clients that are useful outside this UI.
- SDK auth handlers, SDK service clients, or reusable compatibility matrices.
- Provider-neutral feature, geometry, scene, routing, offline sync, plugin, or
  field validation contracts.
- Canonical gRPC `.proto` definitions. Those stay in `geospatial-grpc`; this UI
  should consume SDK clients or generated bindings rather than carrying protocol
  definitions.

## Mismatch Checks

- If a DTO mirrors a stable server API and is not page-specific, check
  `Honua.Sdk.Admin` first.
- If a typed `HttpClient` method would be useful from a console app, CI tool,
  mobile app, or server automation, it belongs in the SDK and this UI should
  consume it through a versioned NuGet package.
- Do not copy SDK source or use long-lived sibling `ProjectReference` links to
  `honua-sdk-dotnet`. Temporary local references need an explicit removal issue.
- If an admin UI feature depends on server functionality, link the
  `honua-server` dependency issue from the admin issue.
- Keep UI-only stubs local, but add fixture tests when a DTO graduates to the
  SDK so both repos validate the same payloads.

## Companion Repos

- `honua-sdk-dotnet`: reusable admin client, contracts, auth boundaries, and
  shared test fixtures.
- `honua-server`: backend API behavior and operator functionality exposed by
  this UI.
