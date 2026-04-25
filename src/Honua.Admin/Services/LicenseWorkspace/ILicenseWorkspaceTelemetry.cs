using System.Collections.Generic;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Telemetry adapter for every observable transition in the license workspace.
/// Mirrors <see cref="SpecWorkspace.ISpecWorkspaceTelemetry"/> so a single
/// shared sink can replace both implementations later.
/// </summary>
public interface ILicenseWorkspaceTelemetry
{
    void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null);

    void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null);
}
