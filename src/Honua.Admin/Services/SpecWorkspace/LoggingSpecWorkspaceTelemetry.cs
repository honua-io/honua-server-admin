using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Honua.Admin.Services.SpecWorkspace;

public sealed class LoggingSpecWorkspaceTelemetry : ISpecWorkspaceTelemetry
{
    private readonly ILogger<LoggingSpecWorkspaceTelemetry> _logger;

    public LoggingSpecWorkspaceTelemetry(ILogger<LoggingSpecWorkspaceTelemetry> logger)
    {
        _logger = logger;
    }

    public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("spec_workspace event={Event}", eventName);
            return;
        }
        _logger.LogInformation("spec_workspace event={Event} props={@Props}", eventName, properties);
    }

    public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("spec_workspace event={Event} elapsed_ms={Elapsed}", eventName, elapsedMillis);
            return;
        }
        _logger.LogInformation("spec_workspace event={Event} elapsed_ms={Elapsed} props={@Props}", eventName, elapsedMillis, properties);
    }
}
