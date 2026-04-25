using System;
using System.Collections.Generic;
using Honua.Admin.Models.DataConnections;

namespace Honua.Admin.Services.DataConnections.Providers;

/// <summary>
/// Pluggable provider descriptor. Provider-specific UI lives in the components
/// referenced here so the workspace shell stays provider-agnostic. New
/// providers register through <see cref="IProviderRegistry"/>; the workspace
/// requires no changes when SQL Server / MySQL / cloud spatial DBs ship
/// concrete implementations.
/// </summary>
public interface IProviderRegistration
{
    string ProviderId { get; }

    string DisplayName { get; }

    string Description { get; }

    int DefaultPort { get; }

    bool IsStub { get; }

    Type CreateFormComponentType { get; }

    Type CapabilityRendererComponentType { get; }

    IReadOnlyList<CapabilityCheck> ManagedHostingChecks { get; }
}

public interface IProviderRegistry
{
    IReadOnlyList<IProviderRegistration> All { get; }

    IProviderRegistration GetById(string providerId);

    bool TryGet(string providerId, out IProviderRegistration registration);
}
