// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using Honua.Admin.Models.Admin;

namespace Honua.Admin.Services.Admin;

public static class AdminRealtimeEventNames
{
    public static readonly string[] RecentError =
    [
        "RecentError",
        "RecentErrorReceived",
        "ErrorReported"
    ];

    public static readonly string[] DeployOperationChanged =
    [
        "DeployOperationChanged",
        "DeployOperationUpdated",
        "DeploymentProgress"
    ];

    public static readonly string[] MigrationStatusChanged =
    [
        "MigrationStatusChanged",
        "MigrationProgress",
        "MigrationStepCompleted"
    ];

    public static readonly string[] ConnectionHealthChanged =
    [
        "ConnectionHealthChanged",
        "DataConnectionHealthChanged",
        "ConnectionHealthResult"
    ];
}

public interface IAdminRealtimeEvents
{
    DateTimeOffset? LastEventReceivedAt { get; }
    event Action<RecentErrorEntry>? RecentErrorReceived;
    event Action<DeployOperation>? DeployOperationChanged;
    event Action<MigrationObservabilityResponse>? MigrationStatusChanged;
    event Action<DataConnectionHealthChangedEvent>? ConnectionHealthChanged;
}

public interface IAdminRealtimeEventPublisher
{
    void PublishRecentError(RecentErrorEntry entry);
    void PublishDeployOperationChanged(DeployOperation operation);
    void PublishMigrationStatusChanged(MigrationObservabilityResponse status);
    void PublishConnectionHealthChanged(DataConnectionHealthChangedEvent health);
}

public sealed class AdminRealtimeEventBus : IAdminRealtimeEvents, IAdminRealtimeEventPublisher
{
    public DateTimeOffset? LastEventReceivedAt { get; private set; }

    public event Action<RecentErrorEntry>? RecentErrorReceived;
    public event Action<DeployOperation>? DeployOperationChanged;
    public event Action<MigrationObservabilityResponse>? MigrationStatusChanged;
    public event Action<DataConnectionHealthChangedEvent>? ConnectionHealthChanged;

    public void PublishRecentError(RecentErrorEntry entry)
    {
        Touch();
        RecentErrorReceived?.Invoke(entry);
    }

    public void PublishDeployOperationChanged(DeployOperation operation)
    {
        Touch();
        DeployOperationChanged?.Invoke(operation);
    }

    public void PublishMigrationStatusChanged(MigrationObservabilityResponse status)
    {
        Touch();
        MigrationStatusChanged?.Invoke(status);
    }

    public void PublishConnectionHealthChanged(DataConnectionHealthChangedEvent health)
    {
        Touch();
        ConnectionHealthChanged?.Invoke(health);
    }

    private void Touch() => LastEventReceivedAt = DateTimeOffset.UtcNow;
}
