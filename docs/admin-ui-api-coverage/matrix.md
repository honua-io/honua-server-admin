# Admin UI ↔ Honua-Server API Coverage Matrix

> **Generated** — do not edit by hand. Run `dotnet run --project tools/audit-api-surface -- render` to regenerate.

- snapshot_date: 2026-04-26
- honua_server_commit: `1b301c3a98e7c97ff75c394852bfc614d5db8a7a`
- endpoints_total: 410

## Coverage Summary

| coverage | count |
| -------- | ----- |
| supported | 41 |
| partial | 0 |
| missing | 44 |
| out-of-scope | 325 |

## Admin

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Admin/AdminEndpoints:GET:/api/v{version:apiVersion}/admin/config` | P0 | supported | /server-info (ServerInfoPage) | Raw tab loads configuration documentation. |
| `Admin/ConfigurationDiscoveryEndpoints:GET:/api/v{version:apiVersion}/admin/configuration/audit` | P0 | supported | /server-info (ServerInfoPage) | Audit tab renders environment, machine, application, and source counts. |
| `Admin/ConfigurationDiscoveryEndpoints:GET:/api/v{version:apiVersion}/admin/configuration/auto-documentation` | P0 | supported | /server-info (ServerInfoPage) | Documentation tab renders generated configuration sections. |
| `Admin/ConfigurationDiscoveryEndpoints:GET:/api/v{version:apiVersion}/admin/configuration/discover` | P0 | supported | /server-info (ServerInfoPage) | Discovery tab renders discovered option types. |
| `Admin/ConfigurationDiscoveryEndpoints:GET:/api/v{version:apiVersion}/admin/configuration/metadata` | P0 | supported | /server-info (ServerInfoPage) | Metadata tab renders section/type/property counts. |
| `Admin/ConfigurationDiscoveryEndpoints:GET:/api/v{version:apiVersion}/admin/configuration/secrets/validate` | P0 | supported | /server-info (ServerInfoPage) | Secrets tab renders validation result and issues. |
| `Admin/ConfigurationDiscoveryEndpoints:GET:/api/v{version:apiVersion}/admin/configuration/summary` | P0 | supported | /server-info (ServerInfoPage) | ServerInfoPage renders environment, type, property, and secret summary cards. |
| `Admin/SecureConnectionEndpoints:GET:/api/v{version:apiVersion}/admin/connections/` | P0 | supported | /connections (ConnectionListPage) | Powers the Connections list. |
| `Admin/SecureConnectionEndpoints:POST:/api/v{version:apiVersion}/admin/connections/` | P0 | supported | /connections/new (CreateConnectionPage) | CreateConnectionPage creates managed-password or secret-reference connections. |
| `Admin/SecureConnectionEndpoints:POST:/api/v{version:apiVersion}/admin/connections/encryption/rotate-key` | P0 | supported | /connections (ConnectionListPage) | Connections toolbar exposes encryption key rotation. |
| `Admin/SecureConnectionEndpoints:POST:/api/v{version:apiVersion}/admin/connections/encryption/validate` | P0 | supported | /connections (ConnectionListPage) | Connections toolbar validates encryption service health. |
| `Admin/SecureConnectionEndpoints:POST:/api/v{version:apiVersion}/admin/connections/test` | P0 | supported | /connections/new (CreateConnectionPage) | CreateConnectionPage can test draft connection credentials. |
| `Admin/SecureConnectionEndpoints:DELETE:/api/v{version:apiVersion}/admin/connections/{id:guid}` | P0 | supported | /connections/{id} (ConnectionDetailPage) | Connection detail exposes delete action with destructive telemetry. |
| `Admin/SecureConnectionEndpoints:GET:/api/v{version:apiVersion}/admin/connections/{id:guid}` | P0 | supported | /connections/{id} (ConnectionDetailPage) | Connection detail loads editable safe connection metadata. |
| `Admin/SecureConnectionEndpoints:PUT:/api/v{version:apiVersion}/admin/connections/{id:guid}` | P0 | supported | /connections/{id} (ConnectionDetailPage) | Connection detail saves editable host/database/user/ssl/active metadata. |
| `Admin/SecureConnectionEndpoints:POST:/api/v{version:apiVersion}/admin/connections/{id:guid}/test` | P0 | supported | /connections/{id} (ConnectionDetailPage) | Connection detail exposes health test action. |
| `Admin/LayerPublishingEndpoints:GET:/api/v{version:apiVersion}/admin/connections/{id}/layers/` | P0 | supported | /layers and /connections/{id} (LayerListPage / ConnectionDetailPage) | Layer pages list published layers for a selected connection/service. |
| `Admin/LayerPublishingEndpoints:POST:/api/v{version:apiVersion}/admin/connections/{id}/layers/` | P0 | supported | /connections/{id}/publish (PublishLayerPage) | PublishLayerPage submits layer publish requests. |
| `Admin/LayerPublishingEndpoints:PUT:/api/v{version:apiVersion}/admin/connections/{id}/layers/enabled` | P0 | supported | /layers (LayerListPage) | LayerListPage bulk enables/disables service layers. |
| `Admin/LayerPublishingEndpoints:PUT:/api/v{version:apiVersion}/admin/connections/{id}/layers/{layerId:int}/enabled` | P0 | supported | /layers and /connections/{id} (LayerListPage / ConnectionDetailPage) | Layer tables expose per-layer enable/disable actions. |
| `Admin/AdminEndpoints:GET:/api/v{version:apiVersion}/admin/connections/{id}/tables` | P0 | supported | /connections/{id} (ConnectionDetailPage) and /connections/{id}/publish (PublishLayerPage) | Discovery tables feed the connection detail and publish-layer workflow. |
| `Admin/DeployControlEndpoints:POST:/api/v{version:apiVersion}/admin/deploy/operations` | P0 | supported | /deploy (DeployControlPage) | Plan tab can create a durable deploy operation. |
| `Admin/DeployControlEndpoints:GET:/api/v{version:apiVersion}/admin/deploy/operations/{operationId}` | P0 | supported | /deploy (DeployControlPage) | Operations tab refreshes durable deploy operation status. |
| `Admin/DeployControlEndpoints:POST:/api/v{version:apiVersion}/admin/deploy/operations/{operationId}/rollback` | P0 | supported | /deploy (DeployControlPage) | Operations tab exposes rollback and records destructive telemetry. |
| `Admin/DeployControlEndpoints:POST:/api/v{version:apiVersion}/admin/deploy/operations/{operationId}/submit` | P0 | supported | /deploy (DeployControlPage) | Operations tab submits awaiting-approval operations. |
| `Admin/DeployControlEndpoints:POST:/api/v{version:apiVersion}/admin/deploy/plan` | P0 | supported | /deploy (DeployControlPage) | Plan tab previews target/current/desired revision planning. |
| `Admin/DeployControlEndpoints:GET:/api/v{version:apiVersion}/admin/deploy/preflight` | P0 | supported | /deploy (DeployControlPage) | Preflight tab runs diagnostics with includeDiagnostics=true. |
| `Admin/FeatureOverviewEndpoints:GET:/api/v{version:apiVersion}/admin/features/` | P0 | supported | / (Index dashboard) | Index dashboard renders edition and feature-gating overview. |
| `Admin/AdminLayerStyleEndpoints:GET:/api/v{version:apiVersion}/admin/metadata/layers/{layerId:int}/style` | P0 | supported | /layers/{layerId}/style (LayerStylePage) | LayerStylePage loads MapLibre and drawingInfo JSON. |
| `Admin/AdminLayerStyleEndpoints:PUT:/api/v{version:apiVersion}/admin/metadata/layers/{layerId:int}/style` | P0 | supported | /layers/{layerId}/style (LayerStylePage) | LayerStylePage saves MapLibre and drawingInfo JSON. |
| `Admin/ObservabilityEndpoints:GET:/api/v{version:apiVersion}/admin/observability/errors` | P0 | supported | /observability (ObservabilityPage) | Errors tab renders the recent-error buffer. |
| `Admin/ObservabilityEndpoints:GET:/api/v{version:apiVersion}/admin/observability/migrations` | P0 | supported | /observability (ObservabilityPage) | Migrations tab renders lifecycle and plan status. |
| `Admin/ObservabilityEndpoints:GET:/api/v{version:apiVersion}/admin/observability/telemetry` | P0 | supported | /observability (ObservabilityPage) | Telemetry tab renders tracing and OTLP status. |
| `Admin/AdminEndpoints:GET:/api/v{version:apiVersion}/admin/openapi.json` | P0 | supported | /server-info (ServerInfoPage) | Raw tab loads the admin OpenAPI document. |
| `Admin/ServiceSettingsEndpoints:GET:/api/v{version:apiVersion}/admin/services/` | P0 | supported | /services (ServiceListPage) | ServiceListPage renders service, layer, and enabled-protocol summary. |
| `Admin/ServiceSettingsEndpoints:PUT:/api/v{version:apiVersion}/admin/services/{serviceName}/access-policy` | P0 | supported | /services/{serviceName}/settings (ServiceSettingsPage) | Access tab updates anonymous and role policy. |
| `Admin/ServiceSettingsEndpoints:PUT:/api/v{version:apiVersion}/admin/services/{serviceName}/layers/{layerId:int}/metadata` | P0 | supported | /services/{serviceName}/settings (ServiceSettingsPage) | Layer metadata tab updates per-layer time metadata. |
| `Admin/ServiceSettingsEndpoints:PUT:/api/v{version:apiVersion}/admin/services/{serviceName}/mapserver` | P0 | supported | /services/{serviceName}/settings (ServiceSettingsPage) | MapServer tab updates rendering defaults and limits. |
| `Admin/ServiceSettingsEndpoints:PUT:/api/v{version:apiVersion}/admin/services/{serviceName}/protocols` | P0 | supported | /services/{serviceName}/settings (ServiceSettingsPage) | Protocols tab updates enabled protocol list. |
| `Admin/ServiceSettingsEndpoints:GET:/api/v{version:apiVersion}/admin/services/{serviceName}/settings` | P0 | supported | /services/{serviceName}/settings (ServiceSettingsPage) | ServiceSettingsPage loads protocol, access, time, and MapServer settings. |
| `Admin/ServiceSettingsEndpoints:PUT:/api/v{version:apiVersion}/admin/services/{serviceName}/timeinfo` | P0 | supported | /services/{serviceName}/settings (ServiceSettingsPage) | Time tab updates service-level time metadata. |
| `Admin/AlertAdminEndpoints:GET:/api/v{version:apiVersion}/admin/alerts/rules` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:POST:/api/v{version:apiVersion}/admin/alerts/rules` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:DELETE:/api/v{version:apiVersion}/admin/alerts/rules/{ruleId:long}` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:PUT:/api/v{version:apiVersion}/admin/alerts/rules/{ruleId:long}` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:GET:/api/v{version:apiVersion}/admin/alerts/zones` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:POST:/api/v{version:apiVersion}/admin/alerts/zones` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:DELETE:/api/v{version:apiVersion}/admin/alerts/zones/{zoneId:long}` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/AlertAdminEndpoints:PUT:/api/v{version:apiVersion}/admin/alerts/zones/{zoneId:long}` | P1 | missing |  | Alert admin pages; deferred to follow-up |
| `Admin/CacheAdminEndpoints:POST:/api/v{version:apiVersion}/admin/cache/invalidate` | P1 | missing |  | Cache invalidation pages; deferred to follow-up |
| `Admin/CacheAdminEndpoints:GET:/api/v{version:apiVersion}/admin/cache/status` | P1 | missing |  | Cache invalidation pages; deferred to follow-up |
| `Admin/AdminStyleSuggestionEndpoints:POST:/api/v{version:apiVersion}/admin/metadata/layers/{layerId:int}/suggest-style` | P1 | missing |  | Style suggestion UX; deferred to follow-up |
| `Admin/OperationsProgressEndpoints:GET:/api/v{version:apiVersion}/admin/operations/active` | P1 | missing |  | Operations progress page; deferred to follow-up |
| `Admin/CacheOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/operations/cache/health` | P1 | missing |  | Cache ops console; deferred to follow-up |
| `Admin/CacheOperationsEndpoints:POST:/api/v{version:apiVersion}/admin/operations/cache/invalidate` | P1 | missing |  | Cache ops console; deferred to follow-up |
| `Admin/OperationsProgressEndpoints:GET:/api/v{version:apiVersion}/admin/operations/type/{operationType}` | P1 | missing |  | Operations progress page; deferred to follow-up |
| `Admin/OperationsProgressEndpoints:GET:/api/v{version:apiVersion}/admin/operations/{operationId}` | P1 | missing |  | Operations progress page; deferred to follow-up |
| `Admin/OperationsProgressEndpoints:POST:/api/v{version:apiVersion}/admin/operations/{operationId}/cancel` | P1 | missing |  | Operations progress page; deferred to follow-up |
| `Admin/RateLimitEndpoints:GET:/api/v{version:apiVersion}/admin/rate-limits/` | P1 | missing |  | Rate-limit admin page; deferred to follow-up |
| `Admin/RateLimitEndpoints:POST:/api/v{version:apiVersion}/admin/rate-limits/` | P1 | missing |  | Rate-limit admin page; deferred to follow-up |
| `Admin/RateLimitEndpoints:GET:/api/v{version:apiVersion}/admin/rate-limits/status` | P1 | missing |  | Rate-limit admin page; deferred to follow-up |
| `Admin/RateLimitEndpoints:DELETE:/api/v{version:apiVersion}/admin/rate-limits/{id:guid}` | P1 | missing |  | Rate-limit admin page; deferred to follow-up |
| `Admin/RateLimitEndpoints:GET:/api/v{version:apiVersion}/admin/rate-limits/{id:guid}` | P1 | missing |  | Rate-limit admin page; deferred to follow-up |
| `Admin/RateLimitEndpoints:PUT:/api/v{version:apiVersion}/admin/rate-limits/{id:guid}` | P1 | missing |  | Rate-limit admin page; deferred to follow-up |
| `Admin/GeocodingOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/operations/geocoding/configuration` | P2 | missing |  | Geocoding ops console |
| `Admin/GeocodingOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/operations/geocoding/providers` | P2 | missing |  | Geocoding ops console |
| `Admin/StreamingOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/operations/streaming/alerts` | P2 | missing |  | Streaming ops console |
| `Admin/StreamingOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/operations/streaming/subscribers` | P2 | missing |  | Streaming ops console |
| `Admin/StreamingOperationsEndpoints:DELETE:/api/v{version:apiVersion}/admin/operations/streaming/subscribers/{subscriberId:guid}` | P2 | missing |  | Streaming ops console |
| `Admin/AdminAuthEndpoints:GET:/api/v{version:apiVersion}/admin/auth/config` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminAuthEndpoints:POST:/api/v{version:apiVersion}/admin/auth/logout` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminAuthEndpoints:POST:/api/v{version:apiVersion}/admin/auth/providers/{providerKey}/authorize-url` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminAuthEndpoints:GET:/api/v{version:apiVersion}/admin/auth/providers/{providerKey}/logout-url` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminAuthEndpoints:POST:/api/v{version:apiVersion}/admin/auth/providers/{providerKey}/token` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminAuthEndpoints:GET:/api/v{version:apiVersion}/admin/auth/session` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminEndpoints:DELETE:/api/v{version:apiVersion}/admin/config` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:PATCH:/api/v{version:apiVersion}/admin/config` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:POST:/api/v{version:apiVersion}/admin/config` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:PUT:/api/v{version:apiVersion}/admin/config` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:GET:/api/v{version:apiVersion}/admin/connections/tables` | n/a | out-of-scope |  | Defensive missing-id error route; the admin UI only calls /connections/{id}/tables with a selected connection |
| `Admin/AdminEndpoints:GET:/api/v{version:apiVersion}/admin/connections/{*path}` | n/a | out-of-scope |  | Defensive catch-all error route; the admin UI only calls concrete admin routes |
| `Admin/AdminEndpoints:DELETE:/api/v{version:apiVersion}/admin/connections/{id}/tables` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:PATCH:/api/v{version:apiVersion}/admin/connections/{id}/tables` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:POST:/api/v{version:apiVersion}/admin/connections/{id}/tables` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:PUT:/api/v{version:apiVersion}/admin/connections/{id}/tables` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/GeocodingAdminEndpoints:GET:/api/v{version:apiVersion}/admin/geocoding/providers` | n/a | out-of-scope |  | Geocoding admin lives with the geocoding-config UI; deferred |
| `Admin/IdentityAdminEndpoints:GET:/api/v{version:apiVersion}/admin/identity/providers` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/IdentityAdminEndpoints:GET:/api/v{version:apiVersion}/admin/identity/providers/{providerType}/test` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/LicenseEndpoints:GET:/api/v{version:apiVersion}/admin/license/` | n/a | out-of-scope |  | Owned by license workspace workstream |
| `Admin/LicenseEndpoints:POST:/api/v{version:apiVersion}/admin/license/` | n/a | out-of-scope |  | Owned by license workspace workstream |
| `Admin/LicenseEndpoints:GET:/api/v{version:apiVersion}/admin/license/entitlements` | n/a | out-of-scope |  | Owned by license workspace workstream |
| `Admin/LicenseAdminEndpoints:GET:/api/v{version:apiVersion}/admin/license/features` | n/a | out-of-scope |  | Owned by license workspace workstream |
| `Admin/LicenseAdminEndpoints:GET:/api/v{version:apiVersion}/admin/license/status` | n/a | out-of-scope |  | Owned by license workspace workstream |
| `Admin/LicenseAdminEndpoints:POST:/api/v{version:apiVersion}/admin/license/upload` | n/a | out-of-scope |  | Owned by license workspace workstream |
| `Admin/OidcProviderEndpoints:GET:/api/v{version:apiVersion}/admin/oidc/providers/` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/OidcProviderEndpoints:POST:/api/v{version:apiVersion}/admin/oidc/providers/` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/OidcProviderEndpoints:DELETE:/api/v{version:apiVersion}/admin/oidc/providers/{id:guid}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/OidcProviderEndpoints:GET:/api/v{version:apiVersion}/admin/oidc/providers/{id:guid}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/OidcProviderEndpoints:PUT:/api/v{version:apiVersion}/admin/oidc/providers/{id:guid}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/OidcProviderEndpoints:POST:/api/v{version:apiVersion}/admin/oidc/providers/{id:guid}/test` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/AdminEndpoints:DELETE:/api/v{version:apiVersion}/admin/openapi.json` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:PATCH:/api/v{version:apiVersion}/admin/openapi.json` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:POST:/api/v{version:apiVersion}/admin/openapi.json` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/AdminEndpoints:PUT:/api/v{version:apiVersion}/admin/openapi.json` | n/a | out-of-scope |  | Generated method-not-allowed handler; admin UI intentionally never invokes unsupported verbs |
| `Admin/RoleEndpoints:GET:/api/v{version:apiVersion}/admin/roles/` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/RoleEndpoints:POST:/api/v{version:apiVersion}/admin/roles/` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/RoleEndpoints:DELETE:/api/v{version:apiVersion}/admin/roles/{id:guid}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/RoleEndpoints:GET:/api/v{version:apiVersion}/admin/roles/{id:guid}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/RoleEndpoints:PUT:/api/v{version:apiVersion}/admin/roles/{id:guid}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/RoleEndpoints:GET:/api/v{version:apiVersion}/admin/roles/{id:guid}/permissions` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/RoleEndpoints:PUT:/api/v{version:apiVersion}/admin/roles/{id:guid}/permissions` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/TileOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/tile-operations/jobs` | n/a | out-of-scope |  | Tile operations admin lives with the map annotations workstream |
| `Admin/TileOperationsEndpoints:POST:/api/v{version:apiVersion}/admin/tile-operations/jobs` | n/a | out-of-scope |  | Tile operations admin lives with the map annotations workstream |
| `Admin/TileOperationsEndpoints:GET:/api/v{version:apiVersion}/admin/tile-operations/jobs/{jobId}` | n/a | out-of-scope |  | Tile operations admin lives with the map annotations workstream |
| `Admin/TileOperationsEndpoints:POST:/api/v{version:apiVersion}/admin/tile-operations/jobs/{jobId}/cancel` | n/a | out-of-scope |  | Tile operations admin lives with the map annotations workstream |
| `Admin/TileOperationsEndpoints:POST:/api/v{version:apiVersion}/admin/tile-operations/jobs/{jobId}/retry` | n/a | out-of-scope |  | Tile operations admin lives with the map annotations workstream |
| `Admin/UserManagementEndpoints:GET:/api/v{version:apiVersion}/admin/users/` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/UserManagementEndpoints:DELETE:/api/v{version:apiVersion}/admin/users/{id}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/UserManagementEndpoints:GET:/api/v{version:apiVersion}/admin/users/{id}` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/UserManagementEndpoints:GET:/api/v{version:apiVersion}/admin/users/{id}/effective-permissions` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |
| `Admin/UserManagementEndpoints:PUT:/api/v{version:apiVersion}/admin/users/{id}/roles` | n/a | out-of-scope |  | Owned by identity/auth admin workstream |

## Export

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Export/ExportEndpoints:GET:/api/v{version:apiVersion}/admin/services/{serviceName}/layers/{layerId:int}/export/` | n/a | out-of-scope |  | Public export endpoint, not admin operations |

## Geocoding

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/GeocodeServer` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/GeocodeServer/findAddressCandidates` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/GeocodeServer/geocodeAddresses` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/GeocodeServer/reverseGeocode` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/GeocodeServer/suggest` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/{locatorName}/GeocodeServer` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/{locatorName}/GeocodeServer/findAddressCandidates` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:POST:/rest/services/{locatorName}/GeocodeServer/findAddressCandidates` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/{locatorName}/GeocodeServer/geocodeAddresses` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:POST:/rest/services/{locatorName}/GeocodeServer/geocodeAddresses` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/{locatorName}/GeocodeServer/reverseGeocode` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:POST:/rest/services/{locatorName}/GeocodeServer/reverseGeocode` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:GET:/rest/services/{locatorName}/GeocodeServer/suggest` | n/a | out-of-scope |  | Public geocoding API surface, not admin |
| `Geocoding/GeocodingEndpoints:POST:/rest/services/{locatorName}/GeocodeServer/suggest` | n/a | out-of-scope |  | Public geocoding API surface, not admin |

## HealthCheck

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `HealthCheck/HealthEndpoints:DELETE:/healthz/live` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:GET:/healthz/live` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:PATCH:/healthz/live` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:POST:/healthz/live` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:PUT:/healthz/live` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:DELETE:/healthz/metrics` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:GET:/healthz/metrics` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:PATCH:/healthz/metrics` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:POST:/healthz/metrics` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:PUT:/healthz/metrics` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:DELETE:/healthz/ready` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:GET:/healthz/ready` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:PATCH:/healthz/ready` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:POST:/healthz/ready` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |
| `HealthCheck/HealthEndpoints:PUT:/healthz/ready` | n/a | out-of-scope |  | Liveness/readiness probes consumed by infra, not admin UI |

## Import

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Import/GeoServerImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/geoserver/discover` | P1 | missing |  | GeoServer import wizard; deferred to follow-up |
| `Import/GeoServerImportEndpoints:GET:/api/v{version:apiVersion}/admin/import/geoserver/jobs` | P1 | missing |  | GeoServer import wizard; deferred to follow-up |
| `Import/GeoServerImportEndpoints:GET:/api/v{version:apiVersion}/admin/import/geoserver/jobs/{jobId}` | P1 | missing |  | GeoServer import wizard; deferred to follow-up |
| `Import/GeoServerImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/geoserver/jobs/{jobId}/cancel` | P1 | missing |  | GeoServer import wizard; deferred to follow-up |
| `Import/GeoServerImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/geoserver/start` | P1 | missing |  | GeoServer import wizard; deferred to follow-up |
| `Import/GeoservicesImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/geoservices/discover` | P1 | missing |  | Geoservices import wizard; deferred to follow-up |
| `Import/GeoservicesImportEndpoints:GET:/api/v{version:apiVersion}/admin/import/geoservices/jobs` | P1 | missing |  | Geoservices import wizard; deferred to follow-up |
| `Import/GeoservicesImportEndpoints:GET:/api/v{version:apiVersion}/admin/import/geoservices/jobs/{jobId}` | P1 | missing |  | Geoservices import wizard; deferred to follow-up |
| `Import/GeoservicesImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/geoservices/jobs/{jobId}/cancel` | P1 | missing |  | Geoservices import wizard; deferred to follow-up |
| `Import/GeoservicesImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/geoservices/start` | P1 | missing |  | Geoservices import wizard; deferred to follow-up |
| `Import/RasterImportEndpoints:POST:/api/v{version:apiVersion}/admin/import/raster/` | P1 | missing |  | Raster import wizard; deferred to follow-up |
| `Import/RasterImportEndpoints:GET:/api/v{version:apiVersion}/admin/import/raster/formats` | P1 | missing |  | Raster import wizard; deferred to follow-up |
| `Import/MigrationScannerEndpoints:POST:/api/v{version:apiVersion}/admin/import/scan` | P1 | missing |  | Migration scanner page; deferred to follow-up |

## Infrastructure

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Infrastructure/StyleEndpoints:GET:/api/styles/{layerId:int}.json` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/DatabasePerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/database/query-cache/statistics` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/cache/effectiveness` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:DELETE:/api/v{version:apiVersion}/admin/performance/enhanced/cache/invalidate` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/cache/statistics` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/database/query-performance` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/database/slow-queries` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/exceptions/recent` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/exceptions/statistics` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/resources/potential-leaks` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:POST:/api/v{version:apiVersion}/admin/performance/enhanced/resources/scan-leaks` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/resources/tracking` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/EnhancedPerformanceEndpoints:GET:/api/v{version:apiVersion}/admin/performance/enhanced/summary` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/MetricsEndpoints:GET:/api/v{version:apiVersion}/metrics/cache` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/MetricsEndpoints:GET:/api/v{version:apiVersion}/metrics/database` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/MetricsEndpoints:GET:/api/v{version:apiVersion}/metrics/health` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/MetricsEndpoints:GET:/api/v{version:apiVersion}/metrics/memory` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/MetricsEndpoints:GET:/api/v{version:apiVersion}/metrics/performance` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/MetricsEndpoints:GET:/api/v{version:apiVersion}/metrics/streaming` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/alerts` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/health/comprehensive` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/health/production` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/metrics/cache` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/metrics/connection-pool` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/metrics/database-resilience` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/metrics/resources` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |
| `Infrastructure/ProductionMonitoringEndpoints:GET:/monitoring/metrics/upload-queue` | n/a | out-of-scope |  | Internal performance/monitoring/metrics surfaces consumed by infra |

## PrintingTools

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `PrintingTools/PrintingToolsEndpoints:GET:/rest/services/Utilities/PrintingTools/GPServer/Export Web Map Task/execute` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |
| `PrintingTools/PrintingToolsEndpoints:POST:/rest/services/Utilities/PrintingTools/GPServer/Export Web Map Task/execute` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |
| `PrintingTools/PrintingToolsEndpoints:GET:/rest/services/Utilities/PrintingTools/GPServer/Export Web Map Task/jobs/{{jobId}}` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |
| `PrintingTools/PrintingToolsEndpoints:GET:/rest/services/Utilities/PrintingTools/GPServer/Export Web Map Task/jobs/{{jobId}}/results/Output_File` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |
| `PrintingTools/PrintingToolsEndpoints:GET:/rest/services/Utilities/PrintingTools/GPServer/Export Web Map Task/submitJob` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |
| `PrintingTools/PrintingToolsEndpoints:POST:/rest/services/Utilities/PrintingTools/GPServer/Export Web Map Task/submitJob` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |
| `PrintingTools/PrintingToolsEndpoints:GET:/rest/services/Utilities/PrintingTools/GPServer/Get Layout Templates Info Task/execute` | n/a | out-of-scope |  | Public ArcGIS-compatible printing API, not admin |

## Protocols

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Protocols/CogEndpoints:GET:/api/v{version:apiVersion}/admin/cloud-rasters/` | n/a | out-of-scope |  | Cloud-COG rendering API, not admin |
| `Protocols/CogEndpoints:POST:/api/v{version:apiVersion}/admin/cloud-rasters/` | n/a | out-of-scope |  | Cloud-COG rendering API, not admin |
| `Protocols/CogEndpoints:DELETE:/api/v{version:apiVersion}/admin/cloud-rasters/{id:long}` | n/a | out-of-scope |  | Cloud-COG rendering API, not admin |
| `Protocols/CogEndpoints:GET:/api/v{version:apiVersion}/admin/cloud-rasters/{id:long}` | n/a | out-of-scope |  | Cloud-COG rendering API, not admin |
| `Protocols/CogEndpoints:POST:/api/v{version:apiVersion}/admin/cloud-rasters/{id:long}/refresh` | n/a | out-of-scope |  | Cloud-COG rendering API, not admin |
| `Protocols/ODataEndpoints:GET:/odata` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:POST:/odata/$batch` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/$metadata` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:POST:/odata/Features` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:DELETE:/odata/Features(LayerId={layerId:int},ObjectId={objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features(LayerId={layerId:int},ObjectId={objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:PATCH:/odata/Features(LayerId={layerId:int},ObjectId={objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:PUT:/odata/Features(LayerId={layerId:int},ObjectId={objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features(LayerId={layerId:int},ObjectId={objectId:long})/$ref` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features(LayerId={layerId:int},ObjectId={objectId:long})/$value` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features({layerId:int})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features({layerId:int})/$apply` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features({layerId:int})/$count` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features({layerId:int})/$search` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:DELETE:/odata/Features({layerId:int},{objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features({layerId:int},{objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:PATCH:/odata/Features({layerId:int},{objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:PUT:/odata/Features({layerId:int},{objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Features/$count` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Layers` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Layers({layerId:int})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Layers({layerId:int})/Features` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:POST:/odata/Layers({layerId:int})/Features` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:DELETE:/odata/Layers({layerId:int})/Features({objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Layers({layerId:int})/Features({objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:PATCH:/odata/Layers({layerId:int})/Features({objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:PUT:/odata/Layers({layerId:int})/Features({objectId:long})` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Layers({layerId:int})/Features/$count` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/ODataEndpoints:GET:/odata/Layers/$count` | n/a | out-of-scope |  | Public OData surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/features` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/features/api` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CollectionsEndpoints:GET:/ogc/features/collections` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CollectionsEndpoints:GET:/ogc/features/collections/{collectionId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/H3Endpoints:GET:/ogc/features/collections/{collectionId}/h3` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/FeaturesEndpoints:GET:/ogc/features/collections/{collectionId}/items` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/FeaturesEndpoints:POST:/ogc/features/collections/{collectionId}/items` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/FeaturesEndpoints:POST:/ogc/features/collections/{collectionId}/items/batch` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/FeaturesEndpoints:DELETE:/ogc/features/collections/{collectionId}/items/{featureId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/FeaturesEndpoints:GET:/ogc/features/collections/{collectionId}/items/{featureId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/FeaturesEndpoints:PUT:/ogc/features/collections/{collectionId}/items/{featureId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CollectionsEndpoints:GET:/ogc/features/collections/{collectionId}/queryables` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/features/conformance` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/collections/{collectionId}/map` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/collections/{collectionId}/map/tiles` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/collections/{collectionId}/map/tiles/{tileMatrixSetId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/collections/{collectionId}/styles/{styleId}/map` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/conformance` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/map` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcMapsEndpoints:GET:/ogc/maps/openapi.json` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/processes/conformance` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/JobEndpoints:GET:/ogc/processes/jobs` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/JobEndpoints:DELETE:/ogc/processes/jobs/{{jobId}}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/JobEndpoints:GET:/ogc/processes/jobs/{{jobId}}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/JobEndpoints:GET:/ogc/processes/jobs/{{jobId}}/results` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/processes/openapi.json` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/ProcessEndpoints:GET:/ogc/processes/processes` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/ProcessEndpoints:GET:/ogc/processes/processes/{{processId}}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/ProcessEndpoints:POST:/ogc/processes/processes/{{processId}}/execution` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcClassicEndpoints:GET:/ogc/services/{serviceId}/wms` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcClassicEndpoints:GET:/ogc/services/{serviceId}/wmts` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/tiles` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CollectionsEndpoints:GET:/ogc/tiles/collections` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CollectionsEndpoints:GET:/ogc/tiles/collections/{collectionId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TilesEndpoints:GET:/ogc/tiles/collections/{collectionId}/tiles` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TilesEndpoints:GET:/ogc/tiles/collections/{collectionId}/tiles/{tileMatrixSetId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TilesEndpoints:GET:/ogc/tiles/collections/{collectionId}/tiles/{tileMatrixSetId}/{tileMatrix}/{tileRow:int}/{tileCol:int}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/tiles/conformance` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/ogc/tiles/openapi.json` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TileMatrixSetEndpoints:GET:/ogc/tiles/tileMatrixSets` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TileMatrixSetEndpoints:GET:/ogc/tiles/tileMatrixSets/{tileMatrixSetId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TilesEndpoints:GET:/ogc/tiles/tiles` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TilesEndpoints:GET:/ogc/tiles/tiles/{tileMatrixSetId}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/TilesEndpoints:GET:/ogc/tiles/tiles/{tileMatrixSetId}/{tileMatrix}/{tileRow:int}/{tileCol:int}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/CoreEndpoints:GET:/openapi.json` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/GeoservicesCatalogEndpoints:GET:/rest/info` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeoservicesCatalogEndpoints:GET:/rest/services` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/areasAndLengths` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/areasAndLengths` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/buffer` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/buffer` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/clip` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/clip` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/difference` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/difference` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/intersect` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/intersect` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/lengths` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/lengths` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/project` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/project` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/simplify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/simplify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:GET:/rest/services/Utilities/Geometry/GeometryServer/union` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GeometryServiceEndpoints:POST:/rest/services/Utilities/Geometry/GeometryServer/union` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/computeClassStatistics` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{id:int}/ImageServer/computeClassStatistics` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/computeStatisticsHistograms` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{id:int}/ImageServer/computeStatisticsHistograms` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/exportImage` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{id:int}/ImageServer/exportImage` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/identify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{id:int}/ImageServer/identify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/legend` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{id:int}/ImageServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{id:int}/ImageServer/tile/{level}/{row}/{col}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/computeClassStatistics` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/computeClassStatistics` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/computeStatisticsHistograms` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/computeStatisticsHistograms` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/exportImage` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/exportImage` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/identify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/identify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/legend` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:POST:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/ImageServerEndpoints:GET:/rest/services/{serviceId:regex(^(?!\\d+$).+$)}/ImageServer/tile/{level}/{row}/{col}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/append` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/applyEdits` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/createReplica` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/extractChanges` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/getEstimates` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/queryDomains` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/relationships` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/replicas` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/replicas/{replicaId}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/synchronizeReplica` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/unRegisterReplica` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/addFeatures` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/append` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/applyEdits` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/calculate` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/calculate` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/deleteFeatures` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/generateRenderer` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/getEstimates` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryBins` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryBins` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryDateBins` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryDateBins` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryH3` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryH3` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryRelatedRecords` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryRelatedRecords` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryTopFeatures` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/queryTopFeatures` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/updateFeatures` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/validateSQL` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/AttachmentEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/{featureId:long}/addAttachment` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/AttachmentEndpoints:GET:/rest/services/{serviceId}/FeatureServer/{layerId:int}/{featureId:long}/attachments` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/AttachmentEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/{featureId:long}/deleteAttachments` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/AttachmentEndpoints:POST:/rest/services/{serviceId}/FeatureServer/{layerId:int}/{featureId:long}/updateAttachment` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:GET:/rest/services/{serviceId}/GPServer/{{taskName}}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:POST:/rest/services/{serviceId}/GPServer/{{taskName}}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:GET:/rest/services/{serviceId}/GPServer/{{taskName}}/jobs/{{jobId}}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:GET:/rest/services/{serviceId}/GPServer/{{taskName}}/jobs/{{jobId}}/cancel` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:POST:/rest/services/{serviceId}/GPServer/{{taskName}}/jobs/{{jobId}}/cancel` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:GET:/rest/services/{serviceId}/GPServer/{{taskName}}/jobs/{{jobId}}/results/{{paramName}}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:GET:/rest/services/{serviceId}/GPServer/{{taskName}}/submitJob` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/GPServerEndpoints:POST:/rest/services/{serviceId}/GPServer/{{taskName}}/submitJob` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/OgcClassicEndpoints:GET:/rest/services/{serviceId}/MapServer/WMS` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcClassicEndpoints:GET:/rest/services/{serviceId}/MapServer/WMTS` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/OgcClassicEndpoints:GET:/rest/services/{serviceId}/MapServer/WMTS/{**restPath}` | n/a | out-of-scope |  | Public OGC API surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/export` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:POST:/rest/services/{serviceId}/MapServer/export` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/find` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:POST:/rest/services/{serviceId}/MapServer/find` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/generateKml` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:POST:/rest/services/{serviceId}/MapServer/generateKml` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/identify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:POST:/rest/services/{serviceId}/MapServer/identify` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/legend` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:POST:/rest/services/{serviceId}/MapServer/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/tile/{z:int}/{y:int}/{x:int}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/{layerId:int}` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:GET:/rest/services/{serviceId}/MapServer/{layerId:int}/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/MapServerEndpoints:POST:/rest/services/{serviceId}/MapServer/{layerId:int}/query` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/CatalogEndpoints:GET:/stac` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/CollectionEndpoints:GET:/stac/collections` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/CollectionEndpoints:GET:/stac/collections/{collectionId}` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/ItemEndpoints:GET:/stac/collections/{collectionId}/items` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/ItemEndpoints:GET:/stac/collections/{collectionId}/items/{itemId}` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/SearchEndpoints:GET:/stac/search` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/SearchEndpoints:POST:/stac/search` | n/a | out-of-scope |  | Public STAC API surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/tiles/{layerId:int}/h3/{z:int}/{x:int}/{y:int}.mvt` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |
| `Protocols/TileJsonEndpoints:GET:/tiles/{layerId:int}/tile.json` | n/a | out-of-scope |  | Public tile API surface, not admin |
| `Protocols/FeatureServerEndpoints:GET:/tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.mvt` | n/a | out-of-scope |  | Public GeoServices REST surface, not admin |

## Spec

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Spec/SpecEndpoints:POST:/v1/spec/apply` | n/a | out-of-scope |  | Spec workspace shipped in #27; admin coverage lives in /operator/spec |
| `Spec/SpecEndpoints:GET:/v1/spec/artifact/{hash}` | n/a | out-of-scope |  | Spec workspace shipped in #27; admin coverage lives in /operator/spec |
| `Spec/SpecEndpoints:POST:/v1/spec/cancel` | n/a | out-of-scope |  | Spec workspace shipped in #27; admin coverage lives in /operator/spec |
| `Spec/SpecEndpoints:POST:/v1/spec/plan` | n/a | out-of-scope |  | Spec workspace shipped in #27; admin coverage lives in /operator/spec |

## StaticMap

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `StaticMap/StaticMapEndpoints:GET:/static/{serviceId}/bbox/{bbox}/{dimensions}.{format}` | n/a | out-of-scope |  | Public static-map rendering API, not admin |
| `StaticMap/StaticMapEndpoints:GET:/static/{serviceId}/{center}/{dimensions}.{format}` | n/a | out-of-scope |  | Public static-map rendering API, not admin |

## Streaming

| key | priority | coverage | admin page | notes |
| --- | -------- | -------- | ---------- | ----- |
| `Streaming/FeatureStreamEndpoints:GET:/api/v{version:apiVersion}/admin/streaming/features/sessions` | P2 | missing |  | Feature stream session admin (mixed admin + public surface) |
| `Streaming/FeatureStreamEndpoints:DELETE:/api/v{version:apiVersion}/admin/streaming/features/sessions/{sessionId:guid}` | P2 | missing |  | Feature stream session admin (mixed admin + public surface) |
| `Streaming/FeatureStreamEndpoints:GET:/api/v{version:apiVersion}/streaming/features` | P2 | missing |  | Feature stream session admin (mixed admin + public surface) |
