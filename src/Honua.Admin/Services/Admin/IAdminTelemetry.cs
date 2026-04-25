// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

namespace Honua.Admin.Services.Admin;

/// <summary>
/// Structured telemetry sink for admin UI events. Mirrors the
/// <c>ISpecWorkspaceTelemetry</c> shape so unit tests can substitute a stub
/// and the production sink stays a thin <c>ILogger</c> adapter.
/// </summary>
public interface IAdminTelemetry
{
    /// <summary>Emit a navigation event when the operator opens a page.</summary>
    void PageNavigated(string pageRoute, string? principalId);

    /// <summary>Emit when the operator triggers a destructive action (delete, apply, submit).</summary>
    void DestructiveAction(string action, string? targetId, string? principalId);

    /// <summary>Emit when an admin client request fails so dashboards can surface error rate.</summary>
    void ClientRequestFailed(string operation, string error);
}
