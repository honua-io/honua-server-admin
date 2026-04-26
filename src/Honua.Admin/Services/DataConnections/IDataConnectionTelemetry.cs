using System.Collections.Generic;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// Telemetry sink for data-connection workspace transitions. Mirrors
/// <c>ISpecWorkspaceTelemetry</c> exactly so the same admin telemetry sink can
/// front both surfaces.
/// </summary>
public interface IDataConnectionTelemetry
{
    void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null);

    void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null);
}
