using System.Collections.Generic;

namespace Honua.Admin.Services.SpecWorkspace;

/// <summary>
/// Telemetry adapter for every observable transition in the spec workspace. Kept
/// behind an interface so a shared admin telemetry sink can replace the default
/// ILogger-backed implementation without touching the state store.
/// </summary>
public interface ISpecWorkspaceTelemetry
{
    void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null);

    void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null);
}
