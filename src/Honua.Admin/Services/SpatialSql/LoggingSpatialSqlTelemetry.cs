using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Honua.Admin.Services.SpatialSql;

public sealed class LoggingSpatialSqlTelemetry : ISpatialSqlTelemetry
{
    private readonly ILogger<LoggingSpatialSqlTelemetry> _logger;

    public LoggingSpatialSqlTelemetry(ILogger<LoggingSpatialSqlTelemetry> logger)
    {
        _logger = logger;
    }

    public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("spatial_sql event={Event}", eventName);
            return;
        }
        _logger.LogInformation("spatial_sql event={Event} props={@Props}", eventName, properties);
    }

    public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("spatial_sql event={Event} elapsed_ms={Elapsed}", eventName, elapsedMillis);
            return;
        }
        _logger.LogInformation("spatial_sql event={Event} elapsed_ms={Elapsed} props={@Props}", eventName, elapsedMillis, properties);
    }
}
