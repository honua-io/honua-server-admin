// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

public sealed record MetadataResourceIdentifier
{
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("namespace")] public string Namespace { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => string.Join(
        "/",
        new[] { Kind, Namespace, Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>
/// Apply request for the manifest endpoint
/// <c>Admin/AdminMetadataEndpoints :: POST /manifest/apply</c>.
/// </summary>
public sealed record ManifestApplyRequest
{
    [JsonPropertyName("resources")] public IReadOnlyList<JsonElement> Resources { get; init; } = Array.Empty<JsonElement>();
    [JsonPropertyName("dryRun")] public bool DryRun { get; init; }
    [JsonPropertyName("prune")] public bool Prune { get; init; }
    [JsonPropertyName("approvalRequired")] public bool ApprovalRequired { get; init; }
    [JsonPropertyName("requestedBy")] public string? RequestedBy { get; init; }
    [JsonPropertyName("requestedReason")] public string? RequestedReason { get; init; }
}

/// <summary>
/// Result of an apply attempt.
/// </summary>
public sealed record ManifestApplyResult
{
    [JsonPropertyName("dryRun")] public bool DryRun { get; init; }
    [JsonPropertyName("summary")] public ManifestApplySummary Summary { get; init; } = new();
    [JsonPropertyName("entries")] public IReadOnlyList<ManifestApplyEntry> Entries { get; init; } = Array.Empty<ManifestApplyEntry>();
}

public sealed record ManifestApplySummary
{
    [JsonPropertyName("created")] public int Created { get; init; }
    [JsonPropertyName("updated")] public int Updated { get; init; }
    [JsonPropertyName("deleted")] public int Deleted { get; init; }
    [JsonPropertyName("skipped")] public int Skipped { get; init; }
}

public sealed record ManifestApplyEntry
{
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("resource")] public MetadataResourceIdentifier Resource { get; init; } = new();
    [JsonPropertyName("message")] public string? Message { get; init; }
}

/// <summary>
/// Drift report from
/// <c>Admin/AdminManifestDriftEndpoints :: GET /manifest/drift</c>.
/// </summary>
public sealed record ManifestDriftReport
{
    [JsonPropertyName("generatedAt")] public DateTimeOffset GeneratedAt { get; init; }
    [JsonPropertyName("baselineVersionId")] public string? BaselineVersionId { get; init; }
    [JsonPropertyName("hasDrift")] public bool HasDrift { get; init; }
    [JsonPropertyName("resources")] public IReadOnlyList<ManifestDriftRecord> Resources { get; init; } = Array.Empty<ManifestDriftRecord>();
}

public sealed record ManifestDriftRecord
{
    [JsonPropertyName("identifier")] public MetadataResourceIdentifier Identifier { get; init; } = new();
    [JsonPropertyName("driftType")] public string DriftType { get; init; } = string.Empty;
    [JsonPropertyName("declaredHash")] public string? DeclaredHash { get; init; }
    [JsonPropertyName("actualHash")] public string? ActualHash { get; init; }
    [JsonPropertyName("declaredSpec")] public JsonElement? DeclaredSpec { get; init; }
    [JsonPropertyName("actualSpec")] public JsonElement? ActualSpec { get; init; }
}

public sealed record ManifestVersionResponse
{
    [JsonPropertyName("versionId")] public string VersionId { get; init; } = string.Empty;
    [JsonPropertyName("manifestHash")] public string ManifestHash { get; init; } = string.Empty;
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("actor")] public string? Actor { get; init; }
    [JsonPropertyName("appliedAt")] public DateTimeOffset AppliedAt { get; init; }
    [JsonPropertyName("resourceCount")] public int ResourceCount { get; init; }
}

public sealed record ManifestVersionDetailResponse
{
    [JsonPropertyName("versionId")] public string VersionId { get; init; } = string.Empty;
    [JsonPropertyName("manifestHash")] public string ManifestHash { get; init; } = string.Empty;
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("actor")] public string? Actor { get; init; }
    [JsonPropertyName("appliedAt")] public DateTimeOffset AppliedAt { get; init; }
    [JsonPropertyName("resourceCount")] public int ResourceCount { get; init; }
    [JsonPropertyName("manifest")] public JsonElement Manifest { get; init; }
}

public sealed record ManifestVersionListResponse
{
    [JsonPropertyName("versions")] public IReadOnlyList<ManifestVersionResponse> Versions { get; init; } = Array.Empty<ManifestVersionResponse>();
}

public sealed record ManifestPendingChangeResponse
{
    [JsonPropertyName("pendingId")] public Guid PendingId { get; init; }
    [JsonPropertyName("manifestHash")] public string ManifestHash { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("requestedBy")] public string? RequestedBy { get; init; }
    [JsonPropertyName("requestedReason")] public string? RequestedReason { get; init; }
    [JsonPropertyName("decisionBy")] public string? DecisionBy { get; init; }
    [JsonPropertyName("decisionReason")] public string? DecisionReason { get; init; }
    [JsonPropertyName("resourceCount")] public int ResourceCount { get; init; }
    [JsonPropertyName("dryRun")] public bool DryRun { get; init; }
    [JsonPropertyName("prune")] public bool Prune { get; init; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("decidedAt")] public DateTimeOffset? DecidedAt { get; init; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record ManifestApproveRequest
{
    [JsonPropertyName("approvedBy")] public string? ApprovedBy { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

public sealed record ManifestRejectRequest
{
    [JsonPropertyName("rejectedBy")] public string? RejectedBy { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

public sealed record GitOpsWatchConfigRequest
{
    [JsonPropertyName("repositoryUrl")] public string RepositoryUrl { get; init; } = string.Empty;
    [JsonPropertyName("branch")] public string Branch { get; init; } = "main";
    [JsonPropertyName("manifestPath")] public string ManifestPath { get; init; } = "manifests/";
    [JsonPropertyName("pollIntervalSeconds")] public int PollIntervalSeconds { get; init; } = 60;
    [JsonPropertyName("approvalRequired")] public bool ApprovalRequired { get; init; }
    [JsonPropertyName("pruneEnabled")] public bool PruneEnabled { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
    [JsonPropertyName("configuredBy")] public string? ConfiguredBy { get; init; }
}

public sealed record GitOpsWatchConfigResponse
{
    [JsonPropertyName("configId")] public Guid ConfigId { get; init; }
    [JsonPropertyName("repositoryUrl")] public string RepositoryUrl { get; init; } = string.Empty;
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("manifestPath")] public string ManifestPath { get; init; } = string.Empty;
    [JsonPropertyName("pollIntervalSeconds")] public int PollIntervalSeconds { get; init; }
    [JsonPropertyName("approvalRequired")] public bool ApprovalRequired { get; init; }
    [JsonPropertyName("pruneEnabled")] public bool PruneEnabled { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("lastKnownCommitSha")] public string? LastKnownCommitSha { get; init; }
    [JsonPropertyName("lastPolledAt")] public DateTimeOffset? LastPolledAt { get; init; }
    [JsonPropertyName("configuredBy")] public string? ConfiguredBy { get; init; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record GitOpsChangeRecordResponse
{
    [JsonPropertyName("changeId")] public Guid ChangeId { get; init; }
    [JsonPropertyName("configId")] public Guid ConfigId { get; init; }
    [JsonPropertyName("commitSha")] public string CommitSha { get; init; } = string.Empty;
    [JsonPropertyName("commitMessage")] public string? CommitMessage { get; init; }
    [JsonPropertyName("commitAuthor")] public string? CommitAuthor { get; init; }
    [JsonPropertyName("commitTimestamp")] public DateTimeOffset? CommitTimestamp { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("pendingApprovalId")] public Guid? PendingApprovalId { get; init; }
    [JsonPropertyName("applySummary")] public string? ApplySummary { get; init; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }
    [JsonPropertyName("detectedAt")] public DateTimeOffset DetectedAt { get; init; }
    [JsonPropertyName("appliedAt")] public DateTimeOffset? AppliedAt { get; init; }
}

public sealed record GitOpsChangeDiffResponse
{
    [JsonPropertyName("changeId")] public Guid ChangeId { get; init; }
    [JsonPropertyName("commitSha")] public string CommitSha { get; init; } = string.Empty;
    [JsonPropertyName("before")] public JsonElement? Before { get; init; }
    [JsonPropertyName("after")] public JsonElement After { get; init; }
}
