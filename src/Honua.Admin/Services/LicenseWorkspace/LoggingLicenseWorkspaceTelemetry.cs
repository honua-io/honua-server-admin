using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Honua.Admin.Services.LicenseWorkspace;

public sealed class LoggingLicenseWorkspaceTelemetry : ILicenseWorkspaceTelemetry
{
    private readonly ILogger<LoggingLicenseWorkspaceTelemetry> _logger;

    public LoggingLicenseWorkspaceTelemetry(ILogger<LoggingLicenseWorkspaceTelemetry> logger)
    {
        _logger = logger;
    }

    public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("license_workspace event={Event}", eventName);
            return;
        }
        _logger.LogInformation("license_workspace event={Event} props={@Props}", eventName, properties);
    }

    public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("license_workspace event={Event} elapsed_ms={Elapsed}", eventName, elapsedMillis);
            return;
        }
        _logger.LogInformation("license_workspace event={Event} elapsed_ms={Elapsed} props={@Props}", eventName, elapsedMillis, properties);
    }
}
