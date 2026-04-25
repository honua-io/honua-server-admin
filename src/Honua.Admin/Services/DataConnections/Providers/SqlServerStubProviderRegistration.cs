using System;
using System.Collections.Generic;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Pages.Operator.DataConnections.Providers;

namespace Honua.Admin.Services.DataConnections.Providers;

/// <summary>
/// Stub registration that proves the registry contract supports a
/// non-Postgres provider without redesign. The form component renders a
/// "coming soon" placeholder; the capability list is empty so renderers stay
/// honest about what is and is not assessed.
/// </summary>
public sealed class SqlServerStubProviderRegistration : IProviderRegistration
{
    public string ProviderId => "sqlserver";

    public string DisplayName => "SQL Server (stub)";

    public string Description => "Pluggable hook only — concrete support arrives with honua-server#362.";

    public int DefaultPort => 1433;

    public bool IsStub => true;

    public Type CreateFormComponentType => typeof(StubProviderForm);

    public Type CapabilityRendererComponentType => typeof(StubCapabilityRenderer);

    public IReadOnlyList<CapabilityCheck> ManagedHostingChecks { get; } = Array.Empty<CapabilityCheck>();
}
