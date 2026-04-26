# Admin UI ↔ honua-server API Coverage

This folder is the audit deliverable for
[honua-server-admin#28](https://github.com/honua-io/honua-server-admin/issues/28).
It answers the question "which honua-server admin endpoints does this admin UI
operate today?" with an artifact future audits can diff against programmatically.

## Layout

| File | Owner | Purpose |
| ---- | ----- | ------- |
| `endpoints.generated.json` | generator | Machine-derived inventory of every `MapGet/MapPost/MapPut/MapDelete/MapPatch`/`MapMethods` call under `honua-server/src/Honua.Server/Features/`, plus the `HonuaFeatureService` gRPC methods. Keys are stable `<feature>/<file>:<verb>:<route>` strings. |
| `coverage.yaml` | humans | One row per endpoint key with `coverage` (supported/partial/missing/out-of-scope), `admin_page`, `priority` (P0/P1/P2/deferred/n/a), `out_of_scope_reason`, `follow_up_ticket`, and `notes`. |
| `matrix.md` | renderer | Reviewable table grouped by feature, regenerated from the join. Always in sync — never hand-edit. |
| `migration-notes.md` | humans | Lineage to the closed PRs and local commits the cherry-picks came from. |

## Workflow

```bash
# 1. Refresh the inventory from honua-server source (sibling checkout discovered
#    automatically; override with --honua-server-root).
dotnet run --project tools/audit-api-surface -- generate

# 2. Add coverage rows for any new keys (preserves edited rows verbatim).
dotnet run --project tools/audit-api-surface -- seed-coverage

# 3. Render the human-readable matrix.
dotnet run --project tools/audit-api-surface -- render
```

The `Honua.Admin.Tests` xunit suite enforces four drift guards
(`tests/Honua.Admin.Tests/Audit/CoverageDriftTests.cs`):

- every key in `endpoints.generated.json` must have a row in `coverage.yaml`;
- every `coverage.yaml` row must reference a key still in the inventory;
- every row uses schema-valid `coverage` and `priority` values, every
  `out-of-scope` row carries an `out_of_scope_reason`, and every `supported`
  row names an `admin_page`;
- when `HONUA_SERVER_PATH` (or a sibling `../honua-server` checkout) is
  available, the generator is re-run and the result is asserted equal to the
  committed JSON. Without honua-server side-by-side the test is a no-op.

## What "supported" means

A row flips to `coverage: supported` only after the admin UI has a page that
actually calls the endpoint and renders something operator-meaningful. P0 rows
default to `missing` and are flipped by hand as cherry-picked pages land.
`out-of-scope` rows must include a written reason — typically pointing at an
adjacent ticket that owns the surface.

## Out-of-scope rationale

- Identity/auth endpoints → `honua-server-admin#22`
- License endpoints → `honua-server-admin#23`
- Tile-operations admin → `honua-server-admin#8`
- Geocoding admin → future ticket (sibling to `#1` SQL playground)
- SQL playground → `honua-server-admin#1` (no generated endpoint row in the
  current inventory)
- Admin quality gates → `honua-server-admin#9` (process/quality workflow, not
  a honua-server endpoint family in the current inventory)
- Public protocol surfaces (OGC, GeoServices, STAC, OData, etc.) → not admin
- Internal performance/monitoring infra → consumed by infra, not admin UI
- gRPC `HonuaFeatureService` → consumed by the Honua SDK, not the admin UI
