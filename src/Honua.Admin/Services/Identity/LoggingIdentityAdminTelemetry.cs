using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Honua.Admin.Services.Identity;

public sealed class LoggingIdentityAdminTelemetry : IIdentityAdminTelemetry
{
    private readonly ILogger<LoggingIdentityAdminTelemetry> _logger;

    public LoggingIdentityAdminTelemetry(ILogger<LoggingIdentityAdminTelemetry> logger)
    {
        _logger = logger;
    }

    public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("identity_admin event={Event}", eventName);
            return;
        }
        _logger.LogInformation("identity_admin event={Event} props={@Props}", eventName, properties);
    }

    public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _logger.LogInformation("identity_admin event={Event} elapsed_ms={Elapsed}", eventName, elapsedMillis);
            return;
        }
        _logger.LogInformation("identity_admin event={Event} elapsed_ms={Elapsed} props={@Props}", eventName, elapsedMillis, properties);
    }

    public void RecordError(string eventName, Exception exception, long elapsedMillis)
    {
        _logger.LogWarning(exception, "identity_admin event={Event} elapsed_ms={Elapsed} status=error", eventName, elapsedMillis);
    }
}
