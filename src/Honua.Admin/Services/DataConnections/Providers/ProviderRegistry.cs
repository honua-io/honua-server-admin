using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Admin.Services.DataConnections.Providers;

public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, IProviderRegistration> _byId;

    public ProviderRegistry(IEnumerable<IProviderRegistration> registrations)
    {
        _byId = registrations.ToDictionary(r => r.ProviderId, StringComparer.OrdinalIgnoreCase);
        All = _byId.Values.OrderBy(r => r.IsStub ? 1 : 0).ThenBy(r => r.DisplayName, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<IProviderRegistration> All { get; }

    public IProviderRegistration GetById(string providerId)
    {
        if (!_byId.TryGetValue(providerId, out var registration))
        {
            throw new KeyNotFoundException($"Provider '{providerId}' is not registered.");
        }
        return registration;
    }

    public bool TryGet(string providerId, out IProviderRegistration registration)
    {
        if (_byId.TryGetValue(providerId, out var found))
        {
            registration = found;
            return true;
        }
        registration = null!;
        return false;
    }
}
