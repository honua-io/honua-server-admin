using System;
using System.Collections.Generic;

namespace Honua.Admin.Services.Identity;

/// <summary>
/// Telemetry adapter for the identity-admin service. Mirrors
/// <see cref="Honua.Admin.Services.SpecWorkspace.ISpecWorkspaceTelemetry"/> so a
/// shared admin sink can replace the default ILogger-backed implementation
/// without touching the client.
/// </summary>
public interface IIdentityAdminTelemetry
{
    void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null);

    void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null);

    void RecordError(string eventName, Exception exception, long elapsedMillis);
}
