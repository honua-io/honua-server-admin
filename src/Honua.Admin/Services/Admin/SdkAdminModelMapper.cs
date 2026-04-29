// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using Honua.Admin.Models.Admin;
using SdkAccessPolicyResponse = Honua.Sdk.Admin.Models.AccessPolicyResponse;
using SdkColumnInfo = Honua.Sdk.Admin.Models.ColumnInfo;
using SdkConnectionDetail = Honua.Sdk.Admin.Models.SecureConnectionDetail;
using SdkConnectionSummary = Honua.Sdk.Admin.Models.SecureConnectionSummary;
using SdkConnectionTestResult = Honua.Sdk.Admin.Models.ConnectionTestResult;
using SdkCreateConnectionRequest = Honua.Sdk.Admin.Models.CreateSecureConnectionRequest;
using SdkCreateDeployOperationRequest = Honua.Sdk.Admin.Models.CreateDeployOperationRequest;
using SdkCreateDeployPlanRequest = Honua.Sdk.Admin.Models.CreateDeployPlanRequest;
using SdkDeployBackendCapabilities = Honua.Sdk.Admin.Models.DeployBackendCapabilities;
using SdkDeployOperation = Honua.Sdk.Admin.Models.DeployOperation;
using SdkDeployPlan = Honua.Sdk.Admin.Models.DeployPlan;
using SdkDeployPlanTarget = Honua.Sdk.Admin.Models.DeployPlanTarget;
using SdkDeployPreflightDatabaseCompatibility = Honua.Sdk.Admin.Models.DeployPreflightDatabaseCompatibility;
using SdkDeployPreflightMigration = Honua.Sdk.Admin.Models.DeployPreflightMigration;
using SdkDeployPreflightReadiness = Honua.Sdk.Admin.Models.DeployPreflightReadiness;
using SdkDeployPreflightResult = Honua.Sdk.Admin.Models.DeployPreflightResult;
using SdkEncryptionValidationResult = Honua.Sdk.Admin.Models.EncryptionValidationResult;
using SdkKeyRotationResult = Honua.Sdk.Admin.Models.KeyRotationResult;
using SdkLayerMetadataResponse = Honua.Sdk.Admin.Models.LayerMetadataResponse;
using SdkLayerStyleResponse = Honua.Sdk.Admin.Models.LayerStyleResponse;
using SdkLayerStyleUpdateRequest = Honua.Sdk.Admin.Models.LayerStyleUpdateRequest;
using SdkMapServerSettingsResponse = Honua.Sdk.Admin.Models.MapServerSettingsResponse;
using SdkMetadataResource = Honua.Sdk.Admin.Models.MetadataResource;
using SdkMetadataResourceResponse = Honua.Sdk.Admin.Models.MetadataResourceResponse;
using SdkMigrationStatus = Honua.Sdk.Admin.Models.MigrationStatus;
using SdkPublishedLayerSummary = Honua.Sdk.Admin.Models.PublishedLayerSummary;
using SdkPublishLayerRequest = Honua.Sdk.Admin.Models.PublishLayerRequest;
using SdkRecentError = Honua.Sdk.Admin.Models.RecentError;
using SdkRecentErrorsResponse = Honua.Sdk.Admin.Models.RecentErrorsResponse;
using SdkResourceMetadata = Honua.Sdk.Admin.Models.ResourceMetadata;
using SdkRollbackDeployOperationRequest = Honua.Sdk.Admin.Models.RollbackDeployOperationRequest;
using SdkServiceSettingsResponse = Honua.Sdk.Admin.Models.ServiceSettingsResponse;
using SdkServiceSummary = Honua.Sdk.Admin.Models.ServiceSummary;
using SdkSubmitDeployOperationRequest = Honua.Sdk.Admin.Models.SubmitDeployOperationRequest;
using SdkTableInfo = Honua.Sdk.Admin.Models.TableInfo;
using SdkTelemetryStatus = Honua.Sdk.Admin.Models.TelemetryStatus;
using SdkTimeInfoResponse = Honua.Sdk.Admin.Models.TimeInfoResponse;
using SdkUpdateAccessPolicyRequest = Honua.Sdk.Admin.Models.UpdateAccessPolicyRequest;
using SdkUpdateConnectionRequest = Honua.Sdk.Admin.Models.UpdateSecureConnectionRequest;
using SdkUpdateLayerMetadataRequest = Honua.Sdk.Admin.Models.UpdateLayerMetadataRequest;
using SdkUpdateMapServerSettingsRequest = Honua.Sdk.Admin.Models.UpdateMapServerSettingsRequest;
using SdkUpdateTimeInfoRequest = Honua.Sdk.Admin.Models.UpdateTimeInfoRequest;

namespace Honua.Admin.Services.Admin;

internal static class SdkAdminModelMapper
{
    public static IReadOnlyList<TOut> MapList<TIn, TOut>(IReadOnlyList<TIn> source, Func<TIn, TOut> map)
    {
        var result = new TOut[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            result[i] = map(source[i]);
        }

        return result;
    }

    public static ConnectionSummary ToConnectionSummary(SdkConnectionSummary connection) => new()
    {
        ConnectionId = connection.ConnectionId,
        Name = connection.Name,
        Description = connection.Description,
        Host = connection.Host,
        Port = connection.Port,
        DatabaseName = connection.DatabaseName,
        Username = connection.Username,
        SslRequired = connection.SslRequired,
        SslMode = connection.SslMode,
        StorageType = connection.StorageType,
        IsActive = connection.IsActive,
        HealthStatus = connection.HealthStatus,
        LastHealthCheck = connection.LastHealthCheck,
        CreatedAt = connection.CreatedAt,
        CreatedBy = connection.CreatedBy,
    };

    public static ConnectionDetail ToConnectionDetail(SdkConnectionDetail connection) => new()
    {
        ConnectionId = connection.ConnectionId,
        Name = connection.Name,
        Description = connection.Description,
        Host = connection.Host,
        Port = connection.Port,
        DatabaseName = connection.DatabaseName,
        Username = connection.Username,
        SslRequired = connection.SslRequired,
        SslMode = connection.SslMode,
        StorageType = connection.StorageType,
        IsActive = connection.IsActive,
        HealthStatus = connection.HealthStatus,
        LastHealthCheck = connection.LastHealthCheck,
        CreatedAt = connection.CreatedAt,
        CreatedBy = connection.CreatedBy,
        CredentialReference = connection.CredentialReference,
        EncryptionVersion = connection.EncryptionVersion,
        UpdatedAt = connection.UpdatedAt,
    };

    public static SdkCreateConnectionRequest ToSdk(CreateConnectionRequest request) => new()
    {
        Name = request.Name,
        Description = request.Description,
        Host = request.Host,
        Port = request.Port,
        DatabaseName = request.DatabaseName,
        Username = request.Username,
        Password = request.Password,
        SecretReference = request.SecretReference,
        SecretType = request.SecretType,
        SslRequired = request.SslRequired,
        SslMode = request.SslMode,
    };

    public static SdkUpdateConnectionRequest ToSdk(UpdateConnectionRequest request) => new()
    {
        Description = request.Description,
        Host = request.Host,
        Port = request.Port,
        DatabaseName = request.DatabaseName,
        Username = request.Username,
        Password = request.Password,
        SslRequired = request.SslRequired,
        SslMode = request.SslMode,
        IsActive = request.IsActive,
    };

    public static TestConnectionResult ToTestConnectionResult(SdkConnectionTestResult result) => new()
    {
        ConnectionId = result.ConnectionId,
        ConnectionName = result.ConnectionName,
        IsHealthy = result.IsHealthy,
        TestedAt = result.TestedAt,
        Message = result.Message ?? string.Empty,
    };

    public static EncryptionValidationResult ToEncryptionValidationResult(SdkEncryptionValidationResult result) => new()
    {
        IsValid = result.IsValid,
        CurrentKeyVersion = result.CurrentKeyVersion,
        ValidatedAt = result.ValidatedAt,
        Message = result.Message ?? string.Empty,
    };

    public static KeyRotationResult ToKeyRotationResult(SdkKeyRotationResult result) => new()
    {
        PreviousKeyVersion = result.PreviousKeyVersion,
        NewKeyVersion = result.NewKeyVersion,
        RotatedAt = result.RotatedAt,
        Message = result.Message ?? string.Empty,
    };

    public static DiscoveredTable ToDiscoveredTable(SdkTableInfo table) => new()
    {
        Schema = table.Schema,
        Table = table.Table,
        GeometryColumn = table.GeometryColumn,
        GeometryType = table.GeometryType,
        Srid = table.Srid,
        EstimatedRows = table.EstimatedRows,
        Columns = MapList(table.Columns, ToTableColumn),
    };

    public static TableColumn ToTableColumn(SdkColumnInfo column) => new()
    {
        Name = column.Name,
        DataType = column.DataType,
        IsNullable = column.IsNullable,
        IsPrimaryKey = column.IsPrimaryKey,
        MaxLength = column.MaxLength,
    };

    public static LayerSummary ToLayerSummary(SdkPublishedLayerSummary layer) => new()
    {
        LayerId = layer.LayerId,
        LayerName = layer.LayerName,
        Schema = layer.Schema,
        Table = layer.Table,
        Description = layer.Description,
        GeometryType = layer.GeometryType,
        Srid = layer.Srid,
        PrimaryKey = layer.PrimaryKey,
        FieldCount = layer.FieldCount,
        Enabled = layer.Enabled,
        ServiceName = layer.ServiceName,
    };

    public static SdkPublishLayerRequest ToSdk(PublishLayerRequest request) => new()
    {
        Schema = request.Schema,
        Table = request.Table,
        LayerName = request.LayerName,
        Description = request.Description,
        GeometryColumn = request.GeometryColumn,
        GeometryType = request.GeometryType,
        Srid = request.Srid,
        PrimaryKey = request.PrimaryKey,
        Fields = request.Fields,
        ServiceName = request.ServiceName,
        Enabled = request.Enabled,
    };

    public static LayerStyle ToLayerStyle(SdkLayerStyleResponse style) => new()
    {
        MapLibreStyle = style.MapLibreStyle,
        DrawingInfo = style.DrawingInfo,
    };

    public static SdkLayerStyleUpdateRequest ToSdk(LayerStyleUpdateRequest request) => new()
    {
        MapLibreStyle = request.MapLibreStyle,
        DrawingInfo = request.DrawingInfo,
    };

    public static ServiceSummary ToServiceSummary(SdkServiceSummary service) => new()
    {
        ServiceName = service.ServiceName,
        Description = service.Description,
        LayerCount = service.LayerCount,
        EnabledProtocols = service.EnabledProtocols ?? Array.Empty<string>(),
    };

    public static ServiceSettings ToServiceSettings(SdkServiceSettingsResponse settings) => new()
    {
        ServiceName = settings.ServiceName,
        EnabledProtocols = settings.EnabledProtocols ?? Array.Empty<string>(),
        AvailableProtocols = settings.AvailableProtocols ?? Array.Empty<string>(),
        AccessPolicy = ToAccessPolicySettings(settings.AccessPolicy),
        TimeInfo = ToTimeInfoSettings(settings.TimeInfo),
        MapServer = ToMapServerSettings(settings.MapServer),
    };

    public static SdkUpdateMapServerSettingsRequest ToSdk(MapServerSettings settings) => new()
    {
        MaxImageWidth = settings.MaxImageWidth,
        MaxImageHeight = settings.MaxImageHeight,
        DefaultImageWidth = settings.DefaultImageWidth,
        DefaultImageHeight = settings.DefaultImageHeight,
        DefaultDpi = settings.DefaultDpi,
        DefaultFormat = settings.DefaultFormat,
        DefaultTransparent = settings.DefaultTransparent,
        MaxFeaturesPerLayer = settings.MaxFeaturesPerLayer,
    };

    public static SdkUpdateAccessPolicyRequest ToSdk(AccessPolicySettings settings) => new()
    {
        AllowAnonymous = settings.AllowAnonymous,
        AllowAnonymousWrite = settings.AllowAnonymousWrite,
        AllowedRoles = ToArrayOrNull(settings.AllowedRoles),
        AllowedWriteRoles = ToArrayOrNull(settings.AllowedWriteRoles),
    };

    public static SdkUpdateTimeInfoRequest ToSdk(TimeInfoSettings settings) => new()
    {
        StartTimeField = settings.StartTimeField,
        EndTimeField = settings.EndTimeField,
        TrackIdField = settings.TrackIdField,
    };

    public static SdkUpdateLayerMetadataRequest ToSdk(UpdateLayerMetadataRequest request) => new()
    {
        AccessPolicy = request.AccessPolicy is null ? null : ToSdk(request.AccessPolicy),
        TimeInfo = request.TimeInfo is null ? null : ToSdk(request.TimeInfo),
    };

    public static LayerMetadataResponse ToLayerMetadataResponse(SdkLayerMetadataResponse response) => new()
    {
        LayerId = response.LayerId,
        LayerName = response.LayerName,
        AccessPolicy = ToAccessPolicySettings(response.AccessPolicy),
        TimeInfo = ToTimeInfoSettings(response.TimeInfo),
    };

    public static MetadataResource ToMetadataResource(SdkMetadataResource resource) => new()
    {
        ApiVersion = resource.ApiVersion,
        Kind = resource.Kind,
        Metadata = ToResourceMetadata(resource.Metadata),
        Spec = resource.Spec,
        Status = resource.Status,
    };

    public static SdkMetadataResource ToSdk(MetadataResource resource) => new()
    {
        ApiVersion = resource.ApiVersion,
        Kind = resource.Kind,
        Metadata = ToSdk(resource.Metadata),
        Spec = resource.Spec,
        Status = resource.Status,
    };

    public static MetadataResourceResponse ToMetadataResourceResponse(SdkMetadataResourceResponse response) => new()
    {
        Resource = ToMetadataResource(response.Resource),
        ETag = response.ETag,
    };

    public static SdkCreateDeployPlanRequest ToSdk(DeployPlanRequest request) => new()
    {
        TargetId = request.TargetId,
        DesiredRevision = request.DesiredRevision,
        CurrentRevision = request.CurrentRevision,
        Parameters = request.Parameters,
    };

    public static SdkCreateDeployOperationRequest ToSdk(CreateDeployOperationRequest request) => new()
    {
        TargetId = request.TargetId,
        DesiredRevision = request.DesiredRevision,
        CurrentRevision = request.CurrentRevision,
        Reason = request.Reason,
        IdempotencyKey = request.IdempotencyKey,
        CorrelationId = request.CorrelationId,
        Priority = request.Priority,
        SubmitImmediately = request.SubmitImmediately,
        Parameters = request.Parameters,
    };

    public static SdkSubmitDeployOperationRequest ToSdk(SubmitDeployOperationRequest request) => new()
    {
        Reason = request.Reason,
    };

    public static SdkRollbackDeployOperationRequest ToSdk(RollbackDeployOperationRequest request) => new()
    {
        Reason = request.Reason,
    };

    public static DeployPreflightResult ToDeployPreflightResult(SdkDeployPreflightResult result) => new()
    {
        Status = result.Status,
        ReadyForCoordinatedDeploy = result.ReadyForCoordinatedDeploy,
        Message = result.Message,
        ServerVersion = result.ServerVersion,
        Environment = result.Environment,
        DeploymentMode = result.DeploymentMode,
        InstanceName = result.InstanceName,
        GeneratedAt = result.GeneratedAt,
        Readiness = ToDeployPreflightReadiness(result.Readiness),
        Migration = ToDeployPreflightMigration(result.Migration),
        DatabaseCompatibility = ToDeployPreflightDatabaseCompatibility(result.DatabaseCompatibility),
    };

    public static DeployPlan ToDeployPlan(SdkDeployPlan plan) => new()
    {
        Target = plan.Target is null ? new DeployPlanTarget() : ToDeployPlanTarget(plan.Target),
        ReadyToSubmit = plan.ReadyToSubmit,
        RequiresApproval = plan.RequiresApproval,
        RequiresOutOfBandMigrations = plan.RequiresOutOfBandMigrations,
        BackendRegistered = plan.BackendRegistered,
        Capabilities = ToDeployBackendCapabilities(plan.Capabilities),
        Warnings = plan.Warnings,
        BlockingReasons = plan.BlockingReasons,
        GeneratedAt = plan.GeneratedAt,
    };

    public static DeployOperation ToDeployOperation(SdkDeployOperation operation) => new()
    {
        OperationId = operation.OperationId,
        Kind = operation.Kind,
        Status = operation.Status,
        Priority = operation.Priority,
        Target = operation.Target is null ? null : ToDeployPlanTarget(operation.Target),
        ProviderOperationId = operation.ProviderOperationId,
        CurrentPhase = operation.CurrentPhase,
        ObservedState = operation.ObservedState,
        ErrorMessage = operation.ErrorMessage,
        Warnings = operation.Warnings,
        BlockingReasons = operation.BlockingReasons,
        RequestedBy = operation.RequestedBy,
        Reason = operation.Reason,
        CorrelationId = operation.CorrelationId,
        CreatedAt = operation.CreatedAt,
        UpdatedAt = operation.UpdatedAt,
        CompletedAt = operation.CompletedAt,
    };

    public static RecentErrorsResponse ToRecentErrorsResponse(SdkRecentErrorsResponse response) => new()
    {
        Capacity = response.Capacity,
        InstanceId = response.InstanceId,
        Errors = MapList(response.Errors, ToRecentErrorEntry),
    };

    public static ObservabilityStatusResponse ToObservabilityStatusResponse(SdkTelemetryStatus status) => new()
    {
        TracingEnabled = status.TracingEnabled,
        OtlpConfigured = status.OtlpConfigured,
        OtlpEndpoint = status.OtlpEndpoint,
    };

    public static MigrationObservabilityResponse ToMigrationObservabilityResponse(SdkMigrationStatus status) => new()
    {
        Status = status.Status,
        IsReady = status.IsReady,
        IsFailed = status.IsFailed,
        Message = status.Message,
        PlanAvailable = status.PlanAvailable,
        UpgradeRequired = status.UpgradeRequired,
        PendingScripts = status.PendingScripts,
        ExecutedButNotDiscoveredScripts = status.ExecutedButNotDiscoveredScripts,
        PlanError = status.PlanError,
        GeneratedAt = status.GeneratedAt,
    };

    private static AccessPolicySettings? ToAccessPolicySettings(SdkAccessPolicyResponse? policy)
    {
        if (policy is null)
        {
            return null;
        }

        return new AccessPolicySettings
        {
            AllowAnonymous = policy.AllowAnonymous,
            AllowAnonymousWrite = policy.AllowAnonymousWrite,
            AllowedRoles = policy.AllowedRoles,
            AllowedWriteRoles = policy.AllowedWriteRoles,
        };
    }

    private static TimeInfoSettings? ToTimeInfoSettings(SdkTimeInfoResponse? timeInfo)
    {
        if (timeInfo is null)
        {
            return null;
        }

        return new TimeInfoSettings
        {
            StartTimeField = timeInfo.StartTimeField,
            EndTimeField = timeInfo.EndTimeField,
            TrackIdField = timeInfo.TrackIdField,
        };
    }

    private static MapServerSettings ToMapServerSettings(SdkMapServerSettingsResponse? settings)
        => settings is null
            ? new MapServerSettings()
            : new MapServerSettings
            {
                MaxImageWidth = settings.MaxImageWidth,
                MaxImageHeight = settings.MaxImageHeight,
                DefaultImageWidth = settings.DefaultImageWidth,
                DefaultImageHeight = settings.DefaultImageHeight,
                DefaultDpi = settings.DefaultDpi,
                DefaultFormat = settings.DefaultFormat,
                DefaultTransparent = settings.DefaultTransparent,
                MaxFeaturesPerLayer = settings.MaxFeaturesPerLayer,
            };

    private static ResourceMetadata? ToResourceMetadata(SdkResourceMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return new ResourceMetadata
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Namespace = metadata.Namespace,
            Labels = metadata.Labels,
            Annotations = metadata.Annotations,
            ResourceVersion = metadata.ResourceVersion,
            Generation = metadata.Generation,
            CreatedAt = metadata.CreatedAt,
            UpdatedAt = metadata.UpdatedAt,
        };
    }

    private static SdkResourceMetadata? ToSdk(ResourceMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return new SdkResourceMetadata
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Namespace = metadata.Namespace,
            Labels = metadata.Labels,
            Annotations = metadata.Annotations,
            ResourceVersion = metadata.ResourceVersion,
            Generation = metadata.Generation,
            CreatedAt = metadata.CreatedAt,
            UpdatedAt = metadata.UpdatedAt,
        };
    }

    private static string[]? ToArrayOrNull(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var result = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static DeployPreflightReadiness? ToDeployPreflightReadiness(SdkDeployPreflightReadiness? readiness)
        => readiness is null
            ? null
            : new DeployPreflightReadiness
            {
                IsReady = readiness.IsReady,
                StatusCode = readiness.StatusCode,
                Message = readiness.Message,
            };

    private static DeployPreflightMigration? ToDeployPreflightMigration(SdkDeployPreflightMigration? migration)
        => migration is null
            ? null
            : new DeployPreflightMigration
            {
                LifecycleStatus = migration.LifecycleStatus,
                Message = migration.Message,
                PlanAvailable = migration.PlanAvailable,
                UpgradeRequired = migration.UpgradeRequired,
                PendingScripts = migration.PendingScripts,
                ExecutedButNotDiscoveredScripts = migration.ExecutedButNotDiscoveredScripts,
                PlanError = migration.PlanError,
            };

    private static DeployPreflightDatabaseCompatibility? ToDeployPreflightDatabaseCompatibility(SdkDeployPreflightDatabaseCompatibility? compatibility)
        => compatibility is null
            ? null
            : new DeployPreflightDatabaseCompatibility
            {
                IsCompatible = compatibility.IsCompatible,
                EngineVersion = compatibility.EngineVersion,
                PostGisVersion = compatibility.PostGisVersion,
                PostGisRasterVersion = compatibility.PostGisRasterVersion,
                InstalledExtensions = compatibility.InstalledExtensions,
                Warnings = compatibility.Warnings,
                ErrorMessage = compatibility.ErrorMessage,
            };

    private static DeployPlanTarget ToDeployPlanTarget(SdkDeployPlanTarget target) => new()
    {
        TargetId = target.TargetId,
        TargetKind = target.TargetKind,
        Backend = target.Backend,
        Environment = target.Environment,
        TargetName = target.TargetName,
        ArtifactReference = target.ArtifactReference,
        RuntimeProfile = target.RuntimeProfile,
        CurrentRevision = target.CurrentRevision,
        DesiredRevision = target.DesiredRevision,
        Parameters = target.Parameters,
    };

    private static DeployBackendCapabilities? ToDeployBackendCapabilities(SdkDeployBackendCapabilities? capabilities)
        => capabilities is null
            ? null
            : new DeployBackendCapabilities
            {
                SupportsRollback = capabilities.SupportsRollback,
                SupportsCancellation = capabilities.SupportsCancellation,
                SupportsTrafficShifting = capabilities.SupportsTrafficShifting,
                RequiresOutOfBandMigrations = capabilities.RequiresOutOfBandMigrations,
                SupportsProgressPolling = capabilities.SupportsProgressPolling,
                SupportsRevisionPinning = capabilities.SupportsRevisionPinning,
            };

    private static RecentErrorEntry ToRecentErrorEntry(SdkRecentError error) => new()
    {
        Timestamp = error.Timestamp,
        CorrelationId = error.CorrelationId,
        Path = error.Path,
        StatusCode = error.StatusCode,
        Message = error.Message,
    };
}
