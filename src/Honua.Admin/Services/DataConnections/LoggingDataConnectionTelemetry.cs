using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Honua.Admin.Services.DataConnections;

public sealed class LoggingDataConnectionTelemetry : IDataConnectionTelemetry
{
    private readonly ILogger<LoggingDataConnectionTelemetry> _logger;

    public LoggingDataConnectionTelemetry(ILogger<LoggingDataConnectionTelemetry> logger)
    {
        _logger = logger;
    }

    public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("data_connections event={Event}", eventName);
            return;
        }
        _logger.LogInformation("data_connections event={Event} props={@Props}", eventName, properties);
    }

    public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("data_connections event={Event} elapsed_ms={Elapsed}", eventName, elapsedMillis);
            return;
        }
        _logger.LogInformation("data_connections event={Event} elapsed_ms={Elapsed} props={@Props}", eventName, elapsedMillis, properties);
    }
}
