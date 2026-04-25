// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Instance-scoped deploy preflight response.
/// </summary>
public sealed record DeployPreflightResult
{
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("readyForCoordinatedDeploy")] public bool ReadyForCoordinatedDeploy { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("serverVersion")] public string? ServerVersion { get; init; }
    [JsonPropertyName("environment")] public string? Environment { get; init; }
    [JsonPropertyName("deploymentMode")] public string? DeploymentMode { get; init; }
    [JsonPropertyName("instanceName")] public string? InstanceName { get; init; }
    [JsonPropertyName("generatedAt")] public DateTimeOffset GeneratedAt { get; init; }
    [JsonPropertyName("readiness")] public DeployPreflightReadiness? Readiness { get; init; }
    [JsonPropertyName("migration")] public DeployPreflightMigration? Migration { get; init; }
    [JsonPropertyName("databaseCompatibility")] public DeployPreflightDatabaseCompatibility? DatabaseCompatibility { get; init; }

    [JsonIgnore] public bool Ready => ReadyForCoordinatedDeploy;
}

public sealed record DeployPreflightReadiness
{
    [JsonPropertyName("isReady")] public bool IsReady { get; init; }
    [JsonPropertyName("statusCode")] public int StatusCode { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed record DeployPreflightMigration
{
    [JsonPropertyName("lifecycleStatus")] public string LifecycleStatus { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("planAvailable")] public bool PlanAvailable { get; init; }
    [JsonPropertyName("upgradeRequired")] public bool UpgradeRequired { get; init; }
    [JsonPropertyName("pendingScripts")] public IReadOnlyList<string> PendingScripts { get; init; } = Array.Empty<string>();
    [JsonPropertyName("executedButNotDiscoveredScripts")] public IReadOnlyList<string> ExecutedButNotDiscoveredScripts { get; init; } = Array.Empty<string>();
    [JsonPropertyName("planError")] public string? PlanError { get; init; }
}

public sealed record DeployPreflightDatabaseCompatibility
{
    [JsonPropertyName("isCompatible")] public bool IsCompatible { get; init; }
    [JsonPropertyName("engineVersion")] public string EngineVersion { get; init; } = string.Empty;
    [JsonPropertyName("postGisVersion")] public string? PostGisVersion { get; init; }
    [JsonPropertyName("postGisRasterVersion")] public string? PostGisRasterVersion { get; init; }
    [JsonPropertyName("installedExtensions")] public IReadOnlyList<string> InstalledExtensions { get; init; } = Array.Empty<string>();
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }
}

public sealed record DeployPlanRequest
{
    [JsonPropertyName("targetId")] public string TargetId { get; init; } = string.Empty;
    [JsonPropertyName("desiredRevision")] public string DesiredRevision { get; init; } = string.Empty;
    [JsonPropertyName("currentRevision")] public string? CurrentRevision { get; init; }
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}

public sealed record CreateDeployOperationRequest
{
    [JsonPropertyName("targetId")] public string TargetId { get; init; } = string.Empty;
    [JsonPropertyName("desiredRevision")] public string DesiredRevision { get; init; } = string.Empty;
    [JsonPropertyName("currentRevision")] public string? CurrentRevision { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("idempotencyKey")] public string? IdempotencyKey { get; init; }
    [JsonPropertyName("correlationId")] public string? CorrelationId { get; init; }
    [JsonPropertyName("priority")] public string? Priority { get; init; }
    [JsonPropertyName("submitImmediately")] public bool? SubmitImmediately { get; init; }
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}

public sealed record SubmitDeployOperationRequest
{
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

public sealed record RollbackDeployOperationRequest
{
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

public sealed record DeployPlan
{
    [JsonPropertyName("target")] public DeployPlanTarget Target { get; init; } = new();
    [JsonPropertyName("readyToSubmit")] public bool ReadyToSubmit { get; init; }
    [JsonPropertyName("requiresApproval")] public bool RequiresApproval { get; init; }
    [JsonPropertyName("requiresOutOfBandMigrations")] public bool RequiresOutOfBandMigrations { get; init; }
    [JsonPropertyName("backendRegistered")] public bool BackendRegistered { get; init; }
    [JsonPropertyName("capabilities")] public DeployBackendCapabilities? Capabilities { get; init; }
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    [JsonPropertyName("blockingReasons")] public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
    [JsonPropertyName("generatedAt")] public DateTimeOffset GeneratedAt { get; init; }

    [JsonIgnore] public string PlanId => $"{Target.TargetId}:{Target.DesiredRevision}";
}

public sealed record DeployPlanTarget
{
    [JsonPropertyName("targetId")] public string TargetId { get; init; } = string.Empty;
    [JsonPropertyName("targetKind")] public string TargetKind { get; init; } = string.Empty;
    [JsonPropertyName("backend")] public string Backend { get; init; } = string.Empty;
    [JsonPropertyName("environment")] public string Environment { get; init; } = string.Empty;
    [JsonPropertyName("targetName")] public string TargetName { get; init; } = string.Empty;
    [JsonPropertyName("artifactReference")] public string? ArtifactReference { get; init; }
    [JsonPropertyName("runtimeProfile")] public string? RuntimeProfile { get; init; }
    [JsonPropertyName("currentRevision")] public string? CurrentRevision { get; init; }
    [JsonPropertyName("desiredRevision")] public string DesiredRevision { get; init; } = string.Empty;
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public sealed record DeployBackendCapabilities
{
    [JsonPropertyName("supportsRollback")] public bool SupportsRollback { get; init; }
    [JsonPropertyName("supportsCancellation")] public bool SupportsCancellation { get; init; }
    [JsonPropertyName("supportsTrafficShifting")] public bool SupportsTrafficShifting { get; init; }
    [JsonPropertyName("requiresOutOfBandMigrations")] public bool RequiresOutOfBandMigrations { get; init; }
    [JsonPropertyName("supportsProgressPolling")] public bool SupportsProgressPolling { get; init; }
    [JsonPropertyName("supportsRevisionPinning")] public bool SupportsRevisionPinning { get; init; }
}

public sealed record DeployOperation
{
    [JsonPropertyName("operationId")] public string OperationId { get; init; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("priority")] public string Priority { get; init; } = string.Empty;
    [JsonPropertyName("target")] public DeployPlanTarget? Target { get; init; }
    [JsonPropertyName("providerOperationId")] public string? ProviderOperationId { get; init; }
    [JsonPropertyName("currentPhase")] public string? CurrentPhase { get; init; }
    [JsonPropertyName("observedState")] public string? ObservedState { get; init; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    [JsonPropertyName("blockingReasons")] public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
    [JsonPropertyName("requestedBy")] public string? RequestedBy { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("correlationId")] public string? CorrelationId { get; init; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; init; }
    [JsonPropertyName("completedAt")] public DateTimeOffset? CompletedAt { get; init; }
}
