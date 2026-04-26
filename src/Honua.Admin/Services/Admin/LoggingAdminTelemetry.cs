// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using Microsoft.Extensions.Logging;

namespace Honua.Admin.Services.Admin;

/// <summary>
/// Default <see cref="IAdminTelemetry"/> sink — emits structured log lines so
/// the events flow through whatever logger backend the host registers. Real
/// dashboards / OTel sinks land via DI swap if/when needed.
/// </summary>
public sealed class LoggingAdminTelemetry : IAdminTelemetry
{
    private readonly ILogger<LoggingAdminTelemetry> _logger;

    public LoggingAdminTelemetry(ILogger<LoggingAdminTelemetry> logger)
    {
        _logger = logger;
    }

    public void PageNavigated(string pageRoute, string? principalId)
    {
        _logger.LogInformation(
            "admin_page_navigated route={PageRoute} principal={PrincipalId}",
            pageRoute,
            principalId ?? "anonymous");
    }

    public void DestructiveAction(string action, string? targetId, string? principalId)
    {
        _logger.LogInformation(
            "admin_destructive_action action={Action} target={TargetId} principal={PrincipalId}",
            action,
            targetId ?? string.Empty,
            principalId ?? "anonymous");
    }

    public void ClientRequestFailed(string operation, string error)
    {
        _logger.LogWarning(
            "admin_client_failed op={Operation} error={Error}",
            operation,
            error);
    }
}
