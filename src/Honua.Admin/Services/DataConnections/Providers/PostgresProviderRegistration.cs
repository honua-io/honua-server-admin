using System;
using System.Collections.Generic;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Pages.Operator.DataConnections.Providers;

namespace Honua.Admin.Services.DataConnections.Providers;

/// <summary>
/// Concrete Postgres registration. Form, capability renderer, and the static
/// managed-Postgres check list (Aurora / Azure DB) all flow through this
/// descriptor. Per the gap report, <see cref="ManagedHostingChecks"/> renders
/// every cell as <see cref="DiagnosticStatus.NotAssessed"/> until
/// <c>honua-server#644</c> exposes per-check signals.
/// </summary>
public sealed class PostgresProviderRegistration : IProviderRegistration
{
    public string ProviderId => "postgres";

    public string DisplayName => "PostgreSQL (managed or self-hosted)";

    public string Description => "Aurora, Azure DB for PostgreSQL, or self-hosted Postgres 13+.";

    public int DefaultPort => 5432;

    public bool IsStub => false;

    public Type CreateFormComponentType => typeof(PostgresConnectionForm);

    public Type CapabilityRendererComponentType => typeof(PostgresCapabilityRenderer);

    public IReadOnlyList<CapabilityCheck> ManagedHostingChecks { get; } = new[]
    {
        new CapabilityCheck("server-version", "Server version >= 13", "Compatibility", "remediation.upgrade-postgres"),
        new CapabilityCheck("ssl-enforced", "SSL enforced for client connections", "Security", "remediation.enable-ssl"),
        new CapabilityCheck("replication-role", "Connection role is primary (not replica)", "Topology", "remediation.target-primary"),
        new CapabilityCheck("postgis-available", "PostGIS extension available", "Capability", "remediation.install-postgis"),
        new CapabilityCheck("pgaudit-available", "pgaudit extension available", "Compliance", "remediation.enable-pgaudit"),
        new CapabilityCheck("aurora-iam-auth", "Aurora: IAM auth permitted (when Aurora-hosted)", "Identity", "remediation.aurora-iam"),
        new CapabilityCheck("azure-aad-auth", "Azure DB: AAD auth permitted (when Azure-hosted)", "Identity", "remediation.azure-aad")
    };
}
