using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class DeployOrchestrationStateTests
{
    [Fact]
    public async Task InitializeAsync_loads_preflight_and_seeds_default_target()
    {
        var state = new DeployOrchestrationState(new RecordingDeployClient());

        await state.InitializeAsync();

        Assert.Equal(DeployOrchestrationStatus.Idle, state.Status);
        Assert.NotNull(state.Preflight);
        var target = Assert.Single(state.Targets);
        Assert.Equal("honua-server", target.TargetId);
        Assert.Equal("latest", target.DesiredRevision);
    }

    [Fact]
    public async Task PlanSelectedAsync_resolves_each_selected_target_through_server_plan_api()
    {
        var client = new RecordingDeployClient();
        var state = new DeployOrchestrationState(client);
        await state.InitializeAsync();
        state.AddOrUpdateTarget("prod-api", "sha256:abc123", "sha256:old");

        await state.PlanSelectedAsync();

        Assert.Equal(DeployOrchestrationStatus.Idle, state.Status);
        Assert.Contains("honua-server", client.PlannedTargets);
        Assert.Contains("prod-api", client.PlannedTargets);
        Assert.All(state.Targets, target => Assert.NotNull(target.Plan));
        Assert.Contains(state.Targets, target =>
            target.TargetId == "prod-api" &&
            target.Plan?.Target.CurrentRevision == "sha256:old" &&
            target.Plan.Target.DesiredRevision == "sha256:abc123");
    }

    [Fact]
    public async Task Create_submit_refresh_and_rollback_selected_track_durable_operation_ids()
    {
        var client = new RecordingDeployClient();
        var state = new DeployOrchestrationState(client);
        await state.InitializeAsync();
        await state.PlanSelectedAsync();

        await state.CreateOperationsForSelectedAsync("promote candidate", submitImmediately: false);
        var target = Assert.Single(state.Targets);
        Assert.NotNull(target.Operation);
        Assert.StartsWith("deploy-honua-server-", target.Operation!.OperationId, StringComparison.Ordinal);
        Assert.Equal("Planned", target.Operation.Status);

        await state.SubmitSelectedAsync("approval granted");
        Assert.Equal("Submitted", target.Operation.Status);
        Assert.Contains(target.Operation.OperationId, client.SubmittedOperationIds);

        await state.RefreshSelectedAsync();
        Assert.Equal("Reconciling", target.Operation.Status);
        Assert.Equal("Provider accepted operation.", target.Operation.CurrentPhase);

        await state.RollbackSelectedAsync("verification failed");
        Assert.Equal("RollbackRequested", target.Operation.Status);
        Assert.Contains(target.Operation.OperationId, client.RollbackOperationIds);
    }

    [Fact]
    public async Task ApplyRealtimeOperation_updates_matching_operation_without_polling()
    {
        var client = new RecordingDeployClient();
        var state = new DeployOrchestrationState(client);
        await state.InitializeAsync();
        await state.PlanSelectedAsync();
        await state.CreateOperationsForSelectedAsync("promote candidate", submitImmediately: false);
        var target = Assert.Single(state.Targets);
        var operationId = target.Operation!.OperationId;

        var applied = state.ApplyRealtimeOperation(Operation(operationId, "Succeeded", "Rollout complete.", reason: null));

        Assert.True(applied);
        Assert.Equal("Succeeded", target.Operation.Status);
        Assert.Equal("Rollout complete.", target.Operation.CurrentPhase);
        Assert.Equal("Realtime operation update", target.LastAction);
    }

    [Fact]
    public void ApplyRealtimeOperation_adds_untracked_operation_target()
    {
        var state = new DeployOrchestrationState(new RecordingDeployClient());

        var applied = state.ApplyRealtimeOperation(new DeployOperation
        {
            OperationId = "op-prod",
            Status = "Reconciling",
            Target = Target("prod-api", "sha256:new", "sha256:old"),
            UpdatedAt = DateTimeOffset.Parse("2026-04-27T10:05:00Z")
        });

        Assert.True(applied);
        var target = Assert.Single(state.Targets);
        Assert.False(target.Selected);
        Assert.Equal("prod-api", target.TargetId);
        Assert.Equal("Reconciling", target.Operation?.Status);
    }

    [Fact]
    public async Task ApplyRealtimeOperation_syncs_existing_target_revision_fields()
    {
        var state = new DeployOrchestrationState(new RecordingDeployClient());
        await state.InitializeAsync();

        var applied = state.ApplyRealtimeOperation(new DeployOperation
        {
            OperationId = "op-external",
            Status = "Reconciling",
            Target = Target("honua-server", "sha256:new", "sha256:old"),
            UpdatedAt = DateTimeOffset.Parse("2026-04-27T10:06:00Z")
        });

        Assert.True(applied);
        var target = Assert.Single(state.Targets);
        Assert.Equal("sha256:new", target.DesiredRevision);
        Assert.Equal("sha256:old", target.CurrentRevision);
        Assert.Equal("Reconciling", target.Operation?.Status);
    }

    [Fact]
    public async Task ApplyRealtimeOperation_ignores_stale_operation_update()
    {
        var state = new DeployOrchestrationState(new RecordingDeployClient());
        await state.InitializeAsync();

        Assert.True(state.ApplyRealtimeOperation(new DeployOperation
        {
            OperationId = "op-external",
            Status = "Succeeded",
            Target = Target("honua-server", "sha256:new", "sha256:old"),
            UpdatedAt = DateTimeOffset.Parse("2026-04-27T10:10:00Z")
        }));

        var applied = state.ApplyRealtimeOperation(new DeployOperation
        {
            OperationId = "op-external",
            Status = "Reconciling",
            Target = Target("honua-server", "sha256:new", "sha256:old"),
            UpdatedAt = DateTimeOffset.Parse("2026-04-27T10:05:00Z")
        });

        Assert.False(applied);
        var target = Assert.Single(state.Targets);
        Assert.Equal("Succeeded", target.Operation?.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T10:10:00Z"), target.Operation?.UpdatedAt);
    }

    [Fact]
    public async Task SubmitSelectedAsync_skips_selected_targets_without_operations()
    {
        var client = new RecordingDeployClient();
        var state = new DeployOrchestrationState(client);
        await state.InitializeAsync();
        await state.PlanSelectedAsync();
        await state.CreateOperationsForSelectedAsync("promote candidate", submitImmediately: false);
        state.AddOrUpdateTarget("prod-api", "sha256:abc123", currentRevision: null);

        await state.SubmitSelectedAsync("approval granted");

        Assert.Null(state.LastError);
        Assert.Equal(DeployOrchestrationStatus.Idle, state.Status);
        var submittedId = Assert.Single(client.SubmittedOperationIds);
        Assert.StartsWith("deploy-honua-server-", submittedId, StringComparison.Ordinal);
        Assert.Contains(state.Targets, target =>
            target.TargetId == "prod-api" &&
            target.Operation is null &&
            target.LastError is null);
    }

    [Fact]
    public async Task InitializeAsync_when_canceled_restores_idle_status_before_rethrowing()
    {
        var state = new DeployOrchestrationState(new CancelingDeployClient(cancelPreflight: true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => state.InitializeAsync());

        Assert.Equal(DeployOrchestrationStatus.Idle, state.Status);
    }

    [Fact]
    public async Task Target_operation_when_canceled_restores_idle_status_before_rethrowing()
    {
        var state = new DeployOrchestrationState(new CancelingDeployClient(cancelPlan: true));
        await state.InitializeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => state.PlanSelectedAsync());

        Assert.Equal(DeployOrchestrationStatus.Idle, state.Status);
    }

    private static DeployOperation Operation(string operationId, string status, string phase, string? reason)
        => new()
        {
            OperationId = operationId,
            Kind = "Deploy",
            Status = status,
            Priority = "Normal",
            Target = Target("honua-server", "latest", currentRevision: null),
            CurrentPhase = phase,
            ObservedState = status,
            Reason = reason,
            CreatedAt = DateTimeOffset.Parse("2026-04-27T10:01:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-04-27T10:02:00Z"),
        };

    private static DeployPlanTarget Target(string targetId, string desiredRevision, string? currentRevision)
        => new()
        {
            TargetId = targetId,
            TargetName = targetId == "honua-server" ? "Honua Server" : "Production API",
            TargetKind = "Kubernetes",
            Backend = "honua-gitops-kubernetes",
            Environment = targetId == "honua-server" ? "dev" : "prod",
            CurrentRevision = currentRevision,
            DesiredRevision = desiredRevision,
        };

    private sealed class RecordingDeployClient : StubHonuaAdminClient
    {
        private int _operationSequence;

        public List<string> PlannedTargets { get; } = [];

        public List<string> SubmittedOperationIds { get; } = [];

        public List<string> RollbackOperationIds { get; } = [];

        public override Task<DeployPlan> PlanDeployAsync(DeployPlanRequest request, CancellationToken cancellationToken)
        {
            PlannedTargets.Add(request.TargetId);
            return Task.FromResult(new DeployPlan
            {
                Target = Target(request.TargetId, request.DesiredRevision, request.CurrentRevision),
                ReadyToSubmit = true,
                BackendRegistered = true,
                Capabilities = new DeployBackendCapabilities
                {
                    SupportsRollback = true,
                    SupportsProgressPolling = true,
                    SupportsTrafficShifting = true,
                    SupportsRevisionPinning = true,
                },
                GeneratedAt = DateTimeOffset.Parse("2026-04-27T10:00:00Z"),
            });
        }

        public override Task<DeployOperation> CreateDeployOperationAsync(CreateDeployOperationRequest request, CancellationToken cancellationToken)
        {
            _operationSequence++;
            return Task.FromResult(new DeployOperation
            {
                OperationId = $"deploy-{request.TargetId}-{_operationSequence}",
                Kind = "Deploy",
                Status = "Planned",
                Priority = "Normal",
                Target = Target(request.TargetId, request.DesiredRevision, request.CurrentRevision),
                Reason = request.Reason,
                CreatedAt = DateTimeOffset.Parse("2026-04-27T10:01:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-04-27T10:01:00Z"),
            });
        }

        public override Task<DeployOperation> SubmitDeployOperationAsync(string operationId, SubmitDeployOperationRequest request, CancellationToken cancellationToken)
        {
            SubmittedOperationIds.Add(operationId);
            return Task.FromResult(Operation(operationId, "Submitted", "Submitted to provider.", request.Reason));
        }

        public override Task<DeployOperation> GetDeployOperationAsync(string operationId, CancellationToken cancellationToken)
            => Task.FromResult(Operation(operationId, "Reconciling", "Provider accepted operation.", reason: null));

        public override Task<DeployOperation> RollbackDeployOperationAsync(string operationId, RollbackDeployOperationRequest request, CancellationToken cancellationToken)
        {
            RollbackOperationIds.Add(operationId);
            return Task.FromResult(Operation(operationId, "RollbackRequested", "Rollback requested.", request.Reason));
        }
    }

    private sealed class CancelingDeployClient(bool cancelPreflight = false, bool cancelPlan = false) : StubHonuaAdminClient
    {
        public override Task<DeployPreflightResult> GetDeployPreflightAsync(CancellationToken cancellationToken)
            => cancelPreflight
                ? Task.FromCanceled<DeployPreflightResult>(new CancellationToken(canceled: true))
                : base.GetDeployPreflightAsync(cancellationToken);

        public override Task<DeployPlan> PlanDeployAsync(DeployPlanRequest request, CancellationToken cancellationToken)
            => cancelPlan
                ? Task.FromCanceled<DeployPlan>(new CancellationToken(canceled: true))
                : base.PlanDeployAsync(request, cancellationToken);
    }
}
