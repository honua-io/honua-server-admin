// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

public sealed record RecentErrorsResponse
{
    [JsonPropertyName("capacity")] public int Capacity { get; init; }
    [JsonPropertyName("instanceId")] public string InstanceId { get; init; } = string.Empty;
    [JsonPropertyName("errors")] public IReadOnlyList<RecentErrorEntry> Errors { get; init; } = Array.Empty<RecentErrorEntry>();
}

public sealed record RecentErrorEntry
{
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }
    [JsonPropertyName("correlationId")] public string CorrelationId { get; init; } = string.Empty;
    [JsonPropertyName("path")] public string Path { get; init; } = string.Empty;
    [JsonPropertyName("statusCode")] public int StatusCode { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed record ObservabilityStatusResponse
{
    [JsonPropertyName("tracingEnabled")] public bool TracingEnabled { get; init; }
    [JsonPropertyName("otlpConfigured")] public bool OtlpConfigured { get; init; }
    [JsonPropertyName("otlpEndpoint")] public string? OtlpEndpoint { get; init; }
}

public sealed record MigrationObservabilityResponse
{
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("isReady")] public bool IsReady { get; init; }
    [JsonPropertyName("isFailed")] public bool IsFailed { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("planAvailable")] public bool PlanAvailable { get; init; }
    [JsonPropertyName("upgradeRequired")] public bool UpgradeRequired { get; init; }
    [JsonPropertyName("pendingScripts")] public IReadOnlyList<string> PendingScripts { get; init; } = Array.Empty<string>();
    [JsonPropertyName("executedButNotDiscoveredScripts")] public IReadOnlyList<string> ExecutedButNotDiscoveredScripts { get; init; } = Array.Empty<string>();
    [JsonPropertyName("planError")] public string? PlanError { get; init; }
    [JsonPropertyName("generatedAt")] public DateTimeOffset GeneratedAt { get; init; }
}
