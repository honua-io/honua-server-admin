using System.Collections.Generic;

namespace Honua.Admin.Services.SpatialSql;

/// <summary>
/// Telemetry adapter for SQL playground events. Mirrors
/// <see cref="Honua.Admin.Services.SpecWorkspace.ISpecWorkspaceTelemetry"/> so a
/// single shared admin sink can replace both implementations once it lands.
/// </summary>
public interface ISpatialSqlTelemetry
{
    void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null);

    void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null);
}
