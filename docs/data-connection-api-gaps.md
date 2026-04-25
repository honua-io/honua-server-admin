# Data connection API gap report

This report enumerates server-side shortfalls discovered while building the
admin-UI data-connection workspace (`honua-io/honua-server-admin#24`). Each
entry names the DTO field or endpoint, the contract acceptance criterion it
blocks, and the follow-up that would close it. The entries are ready to be
folded into the API audit matrix tracked in `honua-io/honua-server-admin#28`.

## Source endpoints inspected

- `GET /api/v1/admin/connections`
- `GET /api/v1/admin/connections/{id}`
- `POST /api/v1/admin/connections`
- `PUT /api/v1/admin/connections/{id}`
- `DELETE /api/v1/admin/connections/{id}`
- `POST /api/v1/admin/connections/test`
- `POST /api/v1/admin/connections/{id}/test`

Authority: `RequireAdminAuthorization` (the same Admin policy the server
applies to every other admin endpoint). The admin UI does not duplicate this
check; failed `401`/`403` responses surface through `ProblemDetails` into a
typed `ConnectionOperationError(Auth)` and a banner alert.

## Gaps

### 1. `ConnectionTestResult` lacks per-step diagnostic codes

- **Today:** the response is `{ connectionId, connectionName, isHealthy,
  testedAt, message }`. `message` is free-form prose.
- **Blocks:** AC "preflight test surfaces a structured diagnostic for every
  common failure mode (DNS / TCP / auth / SSL / capability / version)".
- **Workaround:** `Services/DataConnections/DiagnosticMapper.cs` distributes
  the single `IsHealthy + Message` signal across six structured cells via a
  narrow heuristic table. Unmatched messages land on `Auth` only; unrelated
  cells render `NotAssessed` rather than `Failed` so we never produce false
  negatives.
- **Server change wanted:** add a `steps[]` collection to
  `ConnectionTestResult` with one entry per `{ step, status, code, message }`.
  The mapper then becomes a pass-through.

### 2. No managed-Postgres certification endpoint (Aurora + Azure DB)

- **Today:** there is no endpoint that reports per-check verdicts for a
  managed-Postgres certification matrix. `honua-io/honua-server#644` tracks
  the work.
- **Blocks:** AC "managed-Postgres validation results (Aurora + Azure DB)
  render with pass / fail per check + remediation copy on failures".
- **Workaround:** the Postgres provider registration exposes a static list of
  expected checks (server version, SSL enforcement, replication role,
  PostGIS, pgaudit, IAM/AAD auth). Until the server endpoint lands, every
  cell renders as `NotAssessed` with a "Awaiting honua-server#644" hover.
- **Server change wanted:** `GET /api/v1/admin/connections/{id}/certification`
  returning `{ providerId, checks[]: { id, status, detail, remediationKey } }`.

### 3. No provider capability registry / `IDataProvider` server abstraction

- **Today:** `DatabaseType="PostgreSQL"` is hard-coded on the server.
  `honua-io/honua-server#362` tracks the multi-database epic.
- **Blocks:** the admin UI registers a SQL Server stub provider to prove the
  pluggable `IProviderRegistration` contract — but no concrete second
  provider can ship until the server returns a `providerId` per
  connection and accepts provider-specific create / update payloads.
- **Server change wanted:** add `providerId` to `SecureConnectionSummary`
  and `SecureConnectionDetail`; surface a `GET /providers` endpoint listing
  the providers the server can actually talk to.

### 4. No masked credential preview on the Summary / Detail DTOs

- **Today:** neither DTO returns a hint for the stored credential. The UI
  knows only `StorageType` (`managed` | `external`) and the optional
  `CredentialReference` for external secrets.
- **Blocks:** richer credential identification in the list view (e.g.,
  "ends in 7af3" tooltip). Today's display is `●●●●●●●● (managed)` with no
  per-record disambiguator.
- **Server change wanted:** add a `maskedHint` field to
  `SecureConnectionSummary` (last-N chars of the encrypted credential or a
  stable derived suffix). Must be safe to expose — never the plaintext.

### 5. DTOs are hand-rolled in the admin repo (no generated client)

- **Today:** `Models/DataConnections/DataConnection.cs`,
  `ConnectionDraft.cs`, and `ConnectionDiagnostic.cs` mirror the server's
  OpenAPI contract bytes-for-bytes by hand. There is no codegen toolchain
  in this repo.
- **Blocks:** schema drift is caught only when the UI breaks at runtime. The
  audit matrix in `#28` is the right home for this duplication concern.
- **Server change wanted:** publish the OpenAPI contract for
  `/api/v1/admin/connections` as part of the SDK package, and add a
  generated-client step in `honua-server-admin` CI. Replace the hand-rolled
  DTOs and the `HttpDataConnectionClient` with the generated client.

### 6. `PUT` is the only path to soft-disable

- **Today:** there is no dedicated `POST /connections/{id}/disable` endpoint;
  the UI sends `PUT … { isActive: false }`. This is what the design brief
  asks for, but it means audit logging on the server cannot distinguish
  "operator disabled the connection" from "operator updated metadata that
  happened to set isActive". Recorded for visibility — not necessarily a
  bug.
- **Blocks:** richer audit history copy on the detail page.
- **Server change wanted:** consider a typed audit reason field on `PUT`,
  or a dedicated disable / enable endpoint.

### 7. No audit-history endpoint for a connection

- **Today:** `Detail.razor` shows only the current-state fields. There is no
  `GET /connections/{id}/history`, so the page cannot show "who created /
  disabled / re-enabled the connection and when".
- **Blocks:** complete answer to the operator question "who broke this and
  when?" — currently the UI surfaces only the latest health status and last
  health-check timestamp.
- **Server change wanted:** `GET /api/v1/admin/connections/{id}/history`
  returning a paged audit log (actor, action, timestamp, before/after).

### 8. `POST` / `PUT` return `SecureConnectionSummary` rather than `SecureConnectionDetail`

- **Today:** the create and update endpoints return only the summary
  projection. `CredentialReference`, `EncryptionVersion`, and `UpdatedAt`
  are not in the response body, so the UI has to issue a follow-up
  `GET /connections/{id}` to render the detail surface accurately after a
  mutating call. `DataConnectionsState.SubmitDraftAsync` /
  `SubmitEditAsync` / `SetActiveAsync` each fire that follow-up via
  `TryRefreshSelectedDetailAsync`.
- **Blocks:** the optimistic single-round-trip flow the rest of the
  workspace assumes. Doubles the latency of every save before the page
  shows accurate Detail-only fields (e.g., `CredentialReference` after the
  operator switches credential mode).
- **Server change wanted:** mutating endpoints should return
  `ApiResponse<SecureConnectionDetail>` (the same shape the GET endpoint
  emits), removing the need for a follow-up GET.
