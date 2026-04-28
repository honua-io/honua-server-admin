# Container E2E tests

Issue
[#19](https://github.com/honua-io/honua-server-admin/issues/19) tracks the
full Docker-backed admin UI E2E suite. The current baseline keeps the fast
in-process TestServer integration tests and adds an opt-in Testcontainers lane
for real PostGIS plus Honua Server coverage.

## CI behavior

`Container E2E Readiness` runs on every pull request. It verifies Docker is
available and runs the `Category=ContainerE2E` test lane.

By default, `HONUA_ADMIN_CONTAINER_E2E=false`, so the test lane exits without
starting containers. Set the repository variable `HONUA_ADMIN_CONTAINER_E2E` to
`true` when a pullable Honua Server image is available for CI. When enabled, the
lane starts PostGIS and Honua Server once, seeds a real spatial table, then
exercises the admin readiness, connection, layer publishing, style, service
settings, metadata resource CRUD with ETag concurrency, manifest dry-run,
observability, and deploy preflight paths through the real `HonuaAdminClient`.

## Configuration

| Variable or secret | Default | Purpose |
| --- | --- | --- |
| `HONUA_ADMIN_CONTAINER_E2E` | `false` | Enables live container startup when truthy (`true`, `1`, `yes`). |
| `HONUA_SERVER_IMAGE` | `ghcr.io/honua-io/honua-server:latest` | Honua Server image used by the E2E fixture. |
| `HONUA_POSTGIS_IMAGE` | `postgis/postgis:18-3.6` | PostGIS image used by the E2E fixture. |
| `HONUA_ADMIN_CONTAINER_API_KEY` | `test-admin-key` | API key passed to the server and admin client. Configure as a secret in CI. |
| `HONUA_POSTGIS_DATABASE` | `honua_integration` | PostGIS database name. |
| `HONUA_POSTGIS_USERNAME` | `honua_test` | PostGIS username. |
| `HONUA_POSTGIS_PASSWORD` | `honua_test` | PostGIS password. |
| `HONUA_ADMIN_CONTAINER_ENCRYPTION_MASTER_KEY` | `container-e2e-master-key-0123456789` | Master key passed to Honua Server for secure connection encryption during the live container run. |
| `HONUA_SERVER_CONTAINER_PORT` | `8080` | HTTP port exposed inside the Honua Server container. |
| `HONUA_SERVER_WAIT_PATH` | `/api/v1/admin/features/` | HTTP path used by Testcontainers readiness checks. |

## Local run

```bash
HONUA_ADMIN_CONTAINER_E2E=true \
HONUA_SERVER_IMAGE=ghcr.io/honua-io/honua-server:latest \
dotnet test tests/Honua.Admin.IntegrationTests/Honua.Admin.IntegrationTests.csproj \
  --filter "Category=ContainerE2E"
```

Keep the container lane focused on release-critical admin flows. The
non-container integration tests remain the fast contract suite for normal local
development.
