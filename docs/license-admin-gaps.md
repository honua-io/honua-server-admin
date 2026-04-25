# License admin gaps

Generated during ticket `honua-io/honua-server-admin#23`. Lists every license
capability the admin UI surface intentionally promises but does not yet ship,
because the corresponding `honua-server` endpoint or response shape does not
exist. This file feeds the `#28` API-coverage audit matrix.

The admin UI already ships the workspace nav entry, three-pane layout,
diagnostic banners, and the BYOL upload + replace flow against the working
server endpoints. Gaps below are about the server side telling the UI more —
not about the UI hiding capability.

## Gaps

| Capability | Surface in admin UI | Server status | Follow-up |
| --- | --- | --- | --- |
| Stable `LicenseValidationCode` enum on `LicenseStatusResponse` | `LicenseDiagnosticClassifier` pattern-matches on the free-form `ValidationState` string ("expired", "signature", "tamper", "verification") to decide between Expired / InvalidSignature / Unknown. | Today only a free-form string. | `honua-server`: add a discriminated `LicenseValidationCode` (e.g. `Valid`, `Expired`, `InvalidSignature`, `MalformedFile`, `Unsupported`). The admin-side classifier will drop string matching and key off the enum. |
| Discriminated upload-failure responses | `Replace_flow_surfaces_invalid_signature_diagnostic_when_server_rejects_post` E2E currently observes a generic `BadRequest` → `Unknown` diagnostic when the server rejects an upload. | Server returns 400 / 500 with a free-form body for every reason (invalid signature, malformed file, expired-on-upload, etc.). | `honua-server`: differentiate signature-failure vs malformed vs expired-on-upload via response code or a typed error envelope so the admin UI can surface a specific `InvalidSignature` (or other) diagnostic on upload failures, not just on the next status read. |
| Duplicate `LicenseAdminEndpoints` set | `HttpLicenseWorkspaceClient` is hard-pinned to the working `LicenseEndpoints` set (`GET /api/v1/admin/license`, `POST /api/v1/admin/license`, `GET /api/v1/admin/license/entitlements`). The newer `LicenseAdminEndpoints` set (with a 501 `POST /license/upload` placeholder) is intentionally avoided. | Two endpoint sets coexist; only one is functional. | `honua-server`: remove or consolidate `LicenseAdminEndpoints`. If the newer shape is intended, document the migration so the admin client can switch in one PR. |
| `IssuanceSource` field on `LicenseStatusResponse` | `LicenseStatusDto.IssuanceSource` is consumed and rendered as the "Issued by" cell on the status pane. Defaults to `LicenseStatusDto.DefaultIssuanceSource` ("BYOL portal") client-side when the server omits the field, so existing servers keep working. | Field does not exist server-side yet. | `honua-server`: add `IssuanceSource` to `LicenseStatusResponse` (defaults to `"BYOL portal"`); marketplace adapters from `honua-io/honua-server#804` populate `"AWS Marketplace"` / `"Azure Marketplace"` directly. |
| Ed25519 signature verification on upload | The replace dialog surfaces explicit copy that signature verification runs server-side and any failure surfaces post-replace via the diagnostic banner. | `InMemoryLicenseManager.ApplyLicenseAsync` is a placeholder — accepts any bytes, returns "Enterprise / all entitlements". | `honua-server` (coordinates with `honua-io/honua-server#338`): complete `ApplyLicenseAsync` with real Ed25519 verification + persistence so the surfaced "valid signature" claim actually holds. |
| Phone-home / revocation channel reachability indicator | `LicenseDiagnostic.EndpointUnreachable` is surfaced from transport / 5xx errors hitting the admin license endpoints. There is no separate signal for the optional licensing phone-home / revocation channel. | No endpoint reports phone-home health distinct from local license endpoint reachability. | `honua-server`: add a phone-home health field on `LicenseStatusResponse` so the admin UI can distinguish "local API up, revocation channel down" from "everything down". |
| Marketplace-aware surfaces | `LicenseStatusDto.IssuanceSource` accommodates marketplace strings without redesign, but the workspace does not yet render marketplace-specific entitlement / metering / subscription-state panes. | Depends on `honua-io/honua-server#804` (unified license + entitlement architecture) and downstream AWS / Azure adapters. | `honua-server-admin`: marketplace-aware license workspace follow-up depends on `honua-server#804`; will populate `IssuanceSource` from server, add marketplace-specific entitlement panes, and add metering/usage + subscription-state lifecycle UI. Out of scope for this ticket per design brief §Scope Out. |

## Test coverage note

The admin repo today only ships xUnit unit tests; no bUnit / Blazor render
test harness is in place. Per the design's pragmatic tradeoff, ticket `#23`
ships:

- xUnit unit tests for `LicenseDiagnosticClassifier` (every diagnostic copy
  branch — Expired / InvalidSignature / EndpointUnreachable /
  AuthenticationFailure / Unknown — plus the IsValid + past-expiry
  short-circuit), `ExpiryBandClassifier` (all six bands plus the
  UTC-day-truncation invariant), `StubLicenseWorkspaceClient` (recorded
  upload calls, error propagation), `LicenseWorkspaceState` (every state
  transition including the no-byte-retention invariant and the
  refresh-after-upload re-fetch), and `HttpLicenseWorkspaceClient` (envelope
  decode + status-code classification + payload-content checks).
- xUnit in-process E2E coverage in `LicenseReplaceFlowEndToEndTests` that
  composes `HttpLicenseWorkspaceClient` + `LicenseWorkspaceState` against a
  recording `HttpMessageHandler` and walks the design-brief AC path
  (Expired → POST replace → GET refresh → Valid), the InvalidSignature
  rejection path, and the EndpointUnreachable 5xx path. This is the design
  brief's "in-process end-to-end through every layer of the admin code"
  resolution for the E2E AC; a browser-level Playwright run is filed below
  as a follow-up.

Browser-render component tests and a browser-level lifecycle E2E depend on
the admin repo gaining a render-testing harness — recorded as a follow-up:

- `honua-server-admin`: bootstrap a browser-level E2E framework
  (Playwright or Microsoft.Playwright.NUnit) so the lifecycle E2E can run
  against a real Blazor render.
