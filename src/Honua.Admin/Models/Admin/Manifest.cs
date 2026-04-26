// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Apply request for the manifest endpoint
/// <c>Admin/AdminManifestApprovalEndpoints :: POST /manifest/pending/apply</c>.
/// </summary>
public sealed record ManifestApplyRequest
{
    [JsonPropertyName("body")] public string Body { get; init; } = string.Empty;
    [JsonPropertyName("dryRun")] public bool DryRun { get; init; }
    [JsonPropertyName("prune")] public bool Prune { get; init; }
}

/// <summary>
/// Result of an apply attempt.
/// </summary>
public sealed record ManifestApplyResult
{
    [JsonPropertyName("dryRun")] public bool DryRun { get; init; }
    [JsonPropertyName("created")] public int Created { get; init; }
    [JsonPropertyName("updated")] public int Updated { get; init; }
    [JsonPropertyName("deleted")] public int Deleted { get; init; }
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = System.Array.Empty<string>();
    [JsonPropertyName("errors")] public IReadOnlyList<string> Errors { get; init; } = System.Array.Empty<string>();
}

/// <summary>
/// Drift report from
/// <c>Admin/AdminManifestDriftEndpoints :: GET /manifest/pending/drift</c>.
/// </summary>
public sealed record ManifestDriftReport
{
    [JsonPropertyName("computedAtUtc")] public string ComputedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("totalChanges")] public int TotalChanges { get; init; }
    [JsonPropertyName("entries")] public IReadOnlyList<ManifestDriftEntry> Entries { get; init; } = System.Array.Empty<ManifestDriftEntry>();
}

public sealed record ManifestDriftEntry
{
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("targetId")] public string TargetId { get; init; } = string.Empty;
    [JsonPropertyName("change")] public string Change { get; init; } = string.Empty;
    [JsonPropertyName("note")] public string? Note { get; init; }
}
