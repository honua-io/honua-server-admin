// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;

namespace Honua.Admin.AuditTools;

/// <summary>
/// Default classification rules applied when seeding coverage.yaml. The rules
/// are keyed by `<Feature>/<File>` so a file can opt into a single default;
/// per-endpoint overrides land in coverage.yaml directly. The rule set is
/// intentionally narrow (one row per endpoint family) so that adding a new
/// honua-server endpoints file forces an explicit classification.
/// </summary>
public static class CoverageRules
{
    public sealed record DefaultClassification(
        string Coverage,
        string Priority,
        string? AdminPage,
        string? OutOfScopeReason,
        string? FollowUpTicket,
        string? Notes);

    private static readonly Dictionary<string, DefaultClassification> _byFeatureFile = new(StringComparer.Ordinal)
    {
        // ── Operator-critical (P0) — the bare minimum for running a Honua server ──
        // Default classification is `missing P0`; the row flips to `supported`
        // by hand-editing coverage.yaml when the cherry-picked admin page lands.
        // The seeder preserves edited rows verbatim on re-run.
        ["Admin/AdminEndpoints"] = MissingP0("Connection table discovery + admin OpenAPI; planned ConnectionDetailPage"),
        ["Admin/SecureConnectionEndpoints"] = MissingP0("Secure connection CRUD; planned ConnectionListPage / CreateConnectionPage"),
        ["Admin/ServiceSettingsEndpoints"] = MissingP0("Service settings; planned ServiceListPage / ServiceSettingsPage"),
        ["Admin/AdminLayerStyleEndpoints"] = MissingP0("Layer style editor; planned LayerStylePage"),
        ["Admin/LayerPublishingEndpoints"] = MissingP0("Layer publishing wizard; planned LayerListPage / PublishLayerPage"),
        ["Admin/DeployControlEndpoints"] = MissingP0("Deploy preflight/plan/ops; planned DeployControlPage"),
        ["Admin/ObservabilityEndpoints"] = MissingP0("Errors/telemetry/migrations; planned ObservabilityPage"),
        ["Admin/FeatureOverviewEndpoints"] = MissingP0("Aggregated dashboard tiles; planned Index dashboard"),
        ["Admin/AdminManifestApprovalEndpoints"] = MissingP0("Manifest apply with dry-run/prune; planned ManifestPage"),
        ["Admin/AdminManifestDriftEndpoints"] = MissingP0("Manifest drift display; planned ManifestPage"),
        ["Admin/ConfigurationDiscoveryEndpoints"] = MissingP0("Server info / configuration discovery; planned ServerInfoPage"),

        // ── Operator-relevant (P1) — surface in this ticket if budget allows ──
        ["Admin/AdminMetadataEndpoints"] = Missing("P1", "Layer/connection metadata edit; deferred to follow-up"),
        ["Admin/MetadataResourceEndpoints"] = Missing("P1", "Metadata resource CRUD; deferred to follow-up"),
        ["Admin/AdminStyleSuggestionEndpoints"] = Missing("P1", "Style suggestion UX; deferred to follow-up"),
        ["Admin/AlertAdminEndpoints"] = Missing("P1", "Alert admin pages; deferred to follow-up"),
        ["Admin/CacheAdminEndpoints"] = Missing("P1", "Cache invalidation pages; deferred to follow-up"),
        ["Admin/CacheOperationsEndpoints"] = Missing("P1", "Cache ops console; deferred to follow-up"),
        ["Admin/FeatureChangeEventsEndpoints"] = Missing("P1", "Change-event audit page; deferred to follow-up"),
        ["Admin/AdminGitOpsWatchEndpoints"] = Missing("P1", "GitOps watch console; deferred to follow-up"),
        ["Admin/OperationsProgressEndpoints"] = Missing("P1", "Operations progress page; deferred to follow-up"),
        ["Admin/RateLimitEndpoints"] = Missing("P1", "Rate-limit admin page; deferred to follow-up"),
        ["Import/ImportEndpoints"] = Missing("P1", "Generic admin import wizard; deferred to follow-up"),
        ["Import/RasterImportEndpoints"] = Missing("P1", "Raster import wizard; deferred to follow-up"),
        ["Import/GeoServerImportEndpoints"] = Missing("P1", "GeoServer import wizard; deferred to follow-up"),
        ["Import/GeoservicesImportEndpoints"] = Missing("P1", "Geoservices import wizard; deferred to follow-up"),
        ["Import/MigrationScannerEndpoints"] = Missing("P1", "Migration scanner page; deferred to follow-up"),

        // ── Long-tail admin (P2) — defer to a future sweep ──
        ["Admin/StreamingOperationsEndpoints"] = Missing("P2", "Streaming ops console"),
        ["Admin/GeocodingOperationsEndpoints"] = Missing("P2", "Geocoding ops console"),

        // ── Adjacent-ticket scope (out-of-scope) — coordinated with sibling tickets ──
        ["Admin/AdminAuthEndpoints"] = OutOfScope("Owned by identity/auth admin workstream", "honua-server-admin#22"),
        ["Admin/IdentityAdminEndpoints"] = OutOfScope("Owned by identity/auth admin workstream", "honua-server-admin#22"),
        ["Admin/OidcProviderEndpoints"] = OutOfScope("Owned by identity/auth admin workstream", "honua-server-admin#22"),
        ["Admin/UserManagementEndpoints"] = OutOfScope("Owned by identity/auth admin workstream", "honua-server-admin#22"),
        ["Admin/RoleEndpoints"] = OutOfScope("Owned by identity/auth admin workstream", "honua-server-admin#22"),
        ["Admin/LicenseEndpoints"] = OutOfScope("Owned by license workspace workstream", "honua-server-admin#23"),
        ["Admin/LicenseAdminEndpoints"] = OutOfScope("Owned by license workspace workstream", "honua-server-admin#23"),
        ["Admin/GeocodingAdminEndpoints"] = OutOfScope("Geocoding admin lives with the geocoding-config UI; deferred", "honua-server-admin#1 (SQL playground sibling) / future geocoding-admin ticket"),
        ["Admin/TileOperationsEndpoints"] = OutOfScope("Tile operations admin lives with the map annotations workstream", "honua-server-admin#8"),

        ["Spec/SpecEndpoints"] = OutOfScope("Spec workspace shipped in #27; admin coverage lives in /operator/spec", "honua-server-admin#26 (delivered via PR #27)"),

        // ── Public API surface (out-of-scope) — not admin endpoints ──
        ["Protocols/Ogc/*"] = OutOfScope("Public OGC API surface, not admin", null),
        ["Protocols/GeoServices/*"] = OutOfScope("Public GeoServices REST surface, not admin", null),
        ["Protocols/Stac/*"] = OutOfScope("Public STAC API surface, not admin", null),
        ["Protocols/Tiles/*"] = OutOfScope("Public tile API surface, not admin", null),
        ["Protocols/OData/*"] = OutOfScope("Public OData surface, not admin", null),
        ["Protocols/SpatialAnalytics/*"] = OutOfScope("Public spatial-analytics surface, not admin", null),
        ["HealthCheck/HealthEndpoints"] = OutOfScope("Liveness/readiness probes consumed by infra, not admin UI", null),
        ["StaticMap/StaticMapEndpoints"] = OutOfScope("Public static-map rendering API, not admin", null),
        ["Geocoding/GeocodingEndpoints"] = OutOfScope("Public geocoding API surface, not admin", null),
        ["PrintingTools/PrintingToolsEndpoints"] = OutOfScope("Public ArcGIS-compatible printing API, not admin", null),
        ["CloudCog/*"] = OutOfScope("Cloud-COG rendering API, not admin", null),
        ["Protocols/Cog/*"] = OutOfScope("Cloud-COG rendering API, not admin", null),
        ["Streaming/StreamingFeatureEndpoints"] = OutOfScope("Public streaming feature endpoint, not admin", null),
        ["Streaming/AdminStreamingEndpoints"] = Missing("P2", "Streaming admin console"),
        ["Streaming/FeatureStreamEndpoints"] = Missing("P2", "Feature stream session admin (mixed admin + public surface)"),
        ["Export/ExportEndpoints"] = OutOfScope("Public export endpoint, not admin operations", null),
        ["Infrastructure/*"] = OutOfScope("Internal performance/monitoring/metrics surfaces consumed by infra", null),
        ["Grpc/HonuaFeatureService"] = OutOfScope("Consumed by Honua SDK clients, not admin UI", null),
    };

    /// <summary>
    /// Resolve the default classification for an endpoint. Specific
    /// `Feature/File` rules win over `Feature/*` wildcards; if no rule
    /// matches the endpoint is left unclassified, which fails the drift
    /// guard so a human triages it.
    /// </summary>
    public static DefaultClassification? Resolve(EndpointEntry endpoint)
    {
        var key = $"{endpoint.Feature}/{endpoint.File}";
        if (_byFeatureFile.TryGetValue(key, out var direct))
        {
            return direct;
        }

        // Walk up the source path, checking `<folder>/*` rules at every level.
        // E.g. for `Protocols/GeoServices/FeatureServer/FooEndpoints.cs` we try
        // `Protocols/GeoServices/FeatureServer/*`, then `Protocols/GeoServices/*`,
        // then `Protocols/*`. The closest match wins.
        var sourcePath = endpoint.SourceFile.Replace('\\', '/');
        var slash = sourcePath.LastIndexOf('/');
        while (slash > 0)
        {
            var folderKey = sourcePath[..slash] + "/*";
            if (_byFeatureFile.TryGetValue(folderKey, out var folderRule))
            {
                return folderRule;
            }
            slash = sourcePath.LastIndexOf('/', slash - 1);
        }

        var featureKey = endpoint.Feature + "/*";
        if (_byFeatureFile.TryGetValue(featureKey, out var feature))
        {
            return feature;
        }

        return null;
    }

    private static DefaultClassification Supported(string priority, string adminPage, string adminPageNote, string? notes = null) =>
        new(Coverage: "supported", Priority: priority, AdminPage: $"{adminPage} ({adminPageNote})",
            OutOfScopeReason: null, FollowUpTicket: null, Notes: notes);

    private static DefaultClassification Missing(string priority, string notes) =>
        new(Coverage: "missing", Priority: priority, AdminPage: null,
            OutOfScopeReason: null, FollowUpTicket: null, Notes: notes);

    private static DefaultClassification MissingP0(string notes) =>
        new(Coverage: "missing", Priority: "P0", AdminPage: null,
            OutOfScopeReason: null, FollowUpTicket: null, Notes: notes);

    private static DefaultClassification OutOfScope(string reason, string? followUp) =>
        new(Coverage: "out-of-scope", Priority: "n/a", AdminPage: null,
            OutOfScopeReason: reason, FollowUpTicket: followUp, Notes: null);
}
