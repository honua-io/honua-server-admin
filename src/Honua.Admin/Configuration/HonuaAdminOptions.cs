// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

namespace Honua.Admin.Configuration;

/// <summary>
/// Configuration for the typed honua-server admin HTTP client. Bound from
/// `appsettings.json` under the `HonuaServer` section so deployments can
/// override the default base URL and (in dev) supply a static API key.
/// In production both are sourced from <c>AdminAuthStateProvider</c> after
/// the operator authenticates.
/// </summary>
public sealed class HonuaAdminOptions
{
    public const string SectionName = "HonuaServer";

    /// <summary>Default per-request timeout (seconds) when the option is unset.</summary>
    public const int DefaultRequestTimeoutSeconds = 30;

    /// <summary>The base address of the honua-server admin API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Optional absolute SignalR admin hub URL. Defaults to BaseUrl + /hubs/admin.</summary>
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>Optional API key; only used when no operator login is in effect.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Default timeout for admin requests (seconds).</summary>
    public int RequestTimeoutSeconds { get; set; } = DefaultRequestTimeoutSeconds;
}
