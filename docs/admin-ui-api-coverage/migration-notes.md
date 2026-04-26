# Migration Notes — Admin UI Coverage Restoration

`honua-server-admin#28` lands the audit deliverable plus the operator-critical
foundations restored from the closed PRs that pre-dated the spec-editor shell.

## Lineage

| Artifact | Source | Local commit |
| -------- | ------ | ------------ |
| Admin shell scaffolding (`AdminPageBase`, `ConfirmDialog`, `EmptyState`, `ErrorBanner`, `LoadingOverlay`, `AdminAuthHandler`, `AdminAuthStateProvider`, `GlobalErrorHandler`) | [honua-server-admin#17](https://github.com/honua-io/honua-server-admin/pull/17) (closed; superseded by #27) | `67ebf3b` |
| Live dashboard tile pattern | same PR, dashboard polish commit | `7beea4e` |
| `HonuaAdminOptions` + `appsettings.json` plumbing | [honua-server-admin#12](https://github.com/honua-io/honua-server-admin/pull/12) (closed; auth-defaults fixes) | `5035ca9` |
| Integration test fixture pattern (in-process equivalent of the Testcontainers fixture) | [honua-server-admin#19/#20](https://github.com/honua-io/honua-server-admin/pull/19) (closed; E2E test scaffolding) | `8f50ef5` |

The original PR #17 cherry-picks targeted `Honua.Sdk.Admin` (an unpublished
SDK). Per the design's hand-rolled HttpClient guidance, the same shape was
re-implemented inline (`Services/Admin/IHonuaAdminClient.cs`,
`HonuaAdminClient.cs`, `StubHonuaAdminClient.cs`, `AdminJsonContext.cs`).
When the SDK ships, swap the implementation behind the interface — no page
changes needed.

The PR #20 Testcontainers fixture similarly depends on the SDK plus Docker.
For this ticket, `tests/Honua.Admin.IntegrationTests/Fixtures/HonuaServerFixture.cs`
hosts a fake honua-server in-process (Microsoft.AspNetCore.TestHost). It
exercises the same HTTP client + JSON contract end-to-end, with no Docker
dependency. Lighting up the full Testcontainers suite is tracked as a
follow-on once the SDK lands.

## What landed in PR #28

- `tools/audit-api-surface/` — endpoint inventory generator, coverage
  seeder, and matrix renderer.
- `docs/admin-ui-api-coverage/{endpoints.generated.json,coverage.yaml,matrix.md,README.md,migration-notes.md}`.
- `Honua.Admin.Tests/Audit/CoverageDriftTests` — four drift guards
  (`EveryEndpointKeyHasACoverageRow`, `EveryCoverageRowReferencesAnInventoryKey`,
  `EveryRowUsesSchemaValidValues`, and the optional
  `GeneratedInventoryMatchesHonuaServerSource` regeneration check when
  honua-server is side-by-side). Matches the count rendered in
  `docs/admin-ui-api-coverage/README.md` and the four `[Fact]` methods in
  `tests/Honua.Admin.Tests/Audit/CoverageDriftTests.cs`.
- `Honua.Admin/Auth/{AdminAuthStateProvider,AdminAuthHandler,GlobalErrorHandler}.cs`.
- `Honua.Admin/Configuration/HonuaAdminOptions.cs` + `wwwroot/appsettings.json`.
- `Honua.Admin/Components/Admin/{ConfirmDialog,EmptyState,ErrorBanner,LoadingOverlay}.razor`.
- `Honua.Admin/Shared/AdminPageBase.cs`.
- `Honua.Admin/Services/Admin/{IHonuaAdminClient,HonuaAdminClient,StubHonuaAdminClient,IAdminTelemetry,LoggingAdminTelemetry}.cs`.
- `Honua.Admin/Models/Admin/` — DTOs for feature overview, configuration
  discovery, secure connections, table discovery, layer publishing, layer
  style, service settings, deploy control, and observability.
- `Honua.Admin/Pages/Index.razor` (replacement) — edition and feature-gating
  overview from `Admin/FeatureOverviewEndpoints`.
- `Honua.Admin/Pages/Admin/ConnectionListPage.razor` — secured connection
  list plus encryption validation and key rotation.
- `Honua.Admin/Pages/Admin/{CreateConnectionPage,ConnectionDetailPage}.razor`
  — connection create/test/detail/update/delete plus table discovery and
  connection-scoped layers.
- `Honua.Admin/Pages/Admin/{LayerListPage,PublishLayerPage,LayerStylePage}.razor`
  — layer list/publish/bulk enable/per-layer enable/style edit.
- `Honua.Admin/Pages/Admin/{ServiceListPage,ServiceSettingsPage}.razor` —
  service list plus protocol, access policy, MapServer, time, and layer
  metadata updates.
- `Honua.Admin/Pages/Admin/DeployControlPage.razor` — preflight, plan,
  create operation, refresh, submit, and rollback.
- `Honua.Admin/Pages/Admin/ObservabilityPage.razor` — recent errors,
  telemetry, and migration status.
- `Honua.Admin/Pages/Admin/ServerInfoPage.razor` — configuration summary,
  metadata, discovery, auto-documentation, secret validation, audit info,
  config documentation, and admin OpenAPI.
- `Honua.Admin/Shared/NavMenu.razor` (rewrite) — grouped MudNavGroup
  entries (Operations, Catalog, Deploy, Observability) replacing dead
  placeholder links.
- `Honua.Admin.Tests/Pages/AdminPageRenderTests.cs` — bunit smoke tests
  for the restored P0 pages plus an error-banner test.
- `Honua.Admin.Tests/Pages/AdminDestructiveTelemetryTests.cs` — pins the
  documented `IAdminTelemetry.DestructiveAction` contract ("delete, apply,
  submit") for the destructive sites: `delete-connection`, `submit-deploy`,
  `rollback-deploy`, `rotate-encryption-key`, and the bulk
  `enable-all-service-layers` / `disable-all-service-layers` flips.
- `Honua.Admin.Tests/Audit/EndpointInventoryGeneratorTests.cs` — guards
  the generator against regressions in each declaration shape this PR
  added support for: generic
  `group.Map(...).WithMetadata(new HttpMethodMetadata(new[] { ... }))`,
  multi-line `MapPost`/`MapMethods`, C# 12 collection-expression
  metadata `.WithMetadata(new HttpMethodMetadata([HttpMethods.X]))`,
  and `string.Empty` route arguments on both `Map` and `MapGet`.
- `Honua.Admin.IntegrationTests/` — scaffold project + representative E2E,
  including a `secured-probe` regression test that asserts `X-API-Key`
  reaches secured server endpoints through the full
  `AddHttpClient + AdminAuthHandler + GlobalErrorHandler` chain.
- `Honua.Admin.sln` — adds the new tools/test projects.

## P0 coverage notes

Every previously-classified P0 row is `coverage: supported` (the
connections, layers, services, deploy, observability, server-info, and
feature-overview families). The P0 rows that became `out-of-scope` are
generated method-not-allowed handlers or defensive invalid-route handlers
(`/config` non-GET, `/openapi.json` non-GET, `/connections/{id}/tables`
non-GET, and missing-id/catch-all table-discovery routes). Those are
intentionally never invoked by the UI; the supported GET rows cover the
operator workflow.

The manifest approval and manifest drift families (`manifest/pending`,
`manifest/pending/{id}`, `manifest/pending/{id}/approve`,
`manifest/pending/{id}/reject`, `manifest/pending/history`,
`manifest/drift`, `manifest/versions`, `manifest/versions/{versionId}`)
are surfaced as `priority: P0`, `coverage: missing` and stay that way for
this ticket — they are tracked as the planned `ManifestPage` follow-up
work so the next admin UI iteration can flip them to `supported`. The
audit deliverable's job is to make the gap diffable; the page itself is
out of scope here.

The inventory generator was extended in this ticket to resolve four
endpoint declaration shapes the prior line-at-a-time scanner silently
skipped:

- generic `group.Map(route, handler).WithMetadata(new HttpMethodMetadata(new[] { ... }))`;
- `MapPost`/`MapMethods` calls whose argument list wraps to subsequent
  lines;
- C# 12 collection-expression metadata
  `.WithMetadata(new HttpMethodMetadata([HttpMethods.X]))`
  (used by `Admin/FeatureChangeEventsEndpoints` and the
  `Streaming/FeatureStreamEndpoints` admin sub-routes);
- `string.Empty` / `String.Empty` route arguments on both the generic
  `group.Map(...)` (`Admin/MetadataResourceEndpoints` list/create) and
  the verb-style `group.MapGet(...)` (`Protocols/.../OgcMapsEndpoints`
  landing page) calls.

That extends coverage to the `manifest/pending`, `manifest/drift`,
`admin/gitops`, `metadata/resources`, `feature-events/replay`, and
`SpatialAnalyticsEndpoints` families. Those rows currently land as
`coverage: missing` with no admin page assignment; the next admin UI
iteration will reclassify them as planned pages and move them through
the `supported`/`out-of-scope` decision the same way the existing rows
did.

## Coordination with sibling tickets

These adjacent admin UI workstreams are explicit `out-of-scope` rows in
`coverage.yaml` so this ticket cannot silently absorb their scope:

- `honua-server-admin#1` — SQL playground
- `honua-server-admin#8` — map annotations + tile operations
- `honua-server-admin#22` — identity / auth admin (`AdminAuthHandler`,
  `AdminAuthStateProvider`, `GlobalErrorHandler` are wired but inert
  until the OIDC swap lands)
- `honua-server-admin#23` — license workspace
- `honua-server-admin#9` — admin quality gates

Any backend gap surfaced by the audit becomes a new `honua-server` ticket
referenced under the row's `follow_up_ticket` column — never absorbed
inline.
