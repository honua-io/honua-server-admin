// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;

namespace Honua.Admin.Services.Admin;

public enum DeployOrchestrationStatus
{
    Idle,
    Loading,
    Planning,
    Creating,
    Submitting,
    Refreshing,
    RollingBack,
    Error
}

public sealed class DeployFleetTarget
{
    public DeployFleetTarget(string targetId, string desiredRevision, string? currentRevision = null)
    {
        TargetId = targetId;
        DesiredRevision = desiredRevision;
        CurrentRevision = currentRevision;
    }

    public string TargetId { get; private set; }

    public string DesiredRevision { get; private set; }

    public string? CurrentRevision { get; private set; }

    public bool Selected { get; set; } = true;

    public DeployPlan? Plan { get; internal set; }

    public DeployOperation? Operation { get; internal set; }

    public string? LastError { get; internal set; }

    public string LastAction { get; internal set; } = "Awaiting plan";

    public DateTimeOffset? LastUpdated { get; internal set; }

    public DeployPlanTarget? ResolvedTarget => Operation?.Target ?? Plan?.Target;

    public string DisplayName => string.IsNullOrWhiteSpace(ResolvedTarget?.TargetName)
        ? TargetId
        : ResolvedTarget!.TargetName;

    public string TargetKind => EmptyAsUnknown(ResolvedTarget?.TargetKind);

    public string Environment => EmptyAsUnknown(ResolvedTarget?.Environment);

    public string Backend => EmptyAsUnknown(ResolvedTarget?.Backend);

    public string PlanState
    {
        get
        {
            if (LastError is not null) return "Error";
            if (Plan is null) return "Not planned";
            if (Plan.BlockingReasons.Count > 0) return "Blocked";
            if (Plan.RequiresApproval) return "Approval required";
            return Plan.ReadyToSubmit ? "Ready" : "Review";
        }
    }

    public string OperationState => Operation?.Status ?? "No operation";

    public bool CanCreateOperation => Plan?.ReadyToSubmit == true && Plan.BlockingReasons.Count == 0;

    public bool CanSubmit => !string.IsNullOrWhiteSpace(Operation?.OperationId);

    public bool CanRefresh => !string.IsNullOrWhiteSpace(Operation?.OperationId);

    public bool CanRollback =>
        !string.IsNullOrWhiteSpace(Operation?.OperationId) &&
        (Plan?.Capabilities?.SupportsRollback ?? true);

    public void UpdateDraft(string targetId, string desiredRevision, string? currentRevision)
    {
        TargetId = targetId;
        DesiredRevision = desiredRevision;
        CurrentRevision = currentRevision;
        Plan = null;
        Operation = null;
        LastError = null;
        LastAction = "Awaiting plan";
        LastUpdated = null;
    }

    internal void SyncRealtimeTarget(DeployPlanTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.TargetId))
        {
            TargetId = target.TargetId;
        }

        if (!string.IsNullOrWhiteSpace(target.DesiredRevision))
        {
            DesiredRevision = target.DesiredRevision;
        }

        CurrentRevision = target.CurrentRevision;
    }

    private static string EmptyAsUnknown(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;
}

public sealed class DeployOrchestrationState
{
    private readonly IHonuaAdminClient _client;
    private readonly List<DeployFleetTarget> _targets = [];

    public DeployOrchestrationState(IHonuaAdminClient client)
    {
        _client = client;
    }

    public DeployOrchestrationStatus Status { get; private set; } = DeployOrchestrationStatus.Idle;

    public string? LastError { get; private set; }

    public DeployPreflightResult? Preflight { get; private set; }

    public IReadOnlyList<DeployFleetTarget> Targets => _targets;

    public IReadOnlyList<DeployFleetTarget> SelectedTargets => _targets
        .Where(target => target.Selected)
        .ToArray();

    public bool HasSelectedTargets => _targets.Any(target => target.Selected);

    public bool HasCreatableTargets => _targets.Any(target => target.Selected && target.CanCreateOperation);

    public bool HasRefreshableOperations => _targets.Any(target => target.Selected && target.CanRefresh);

    public bool HasSubmittableOperations => _targets.Any(target => target.Selected && target.CanSubmit);

    public bool HasRollbackOperations => _targets.Any(target => target.Selected && target.CanRollback);

    public event Action? OnChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        SetStatus(DeployOrchestrationStatus.Loading);

        try
        {
            Preflight = await _client.GetDeployPreflightAsync(cancellationToken).ConfigureAwait(false);
            if (_targets.Count == 0)
            {
                _targets.Add(new DeployFleetTarget("honua-server", "latest"));
            }

            LastError = null;
            Status = DeployOrchestrationStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            ResetAfterCancellation();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = DeployOrchestrationStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public bool AddOrUpdateTarget(string? targetId, string? desiredRevision, string? currentRevision)
    {
        var normalizedTargetId = NormalizeRequired(targetId);
        var normalizedDesiredRevision = NormalizeRequired(desiredRevision);
        var normalizedCurrentRevision = NormalizeOptional(currentRevision);

        if (normalizedTargetId is null || normalizedDesiredRevision is null)
        {
            LastError = "Target ID and desired revision are required.";
            Notify();
            return false;
        }

        var existing = _targets.FirstOrDefault(target =>
            string.Equals(target.TargetId, normalizedTargetId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.UpdateDraft(normalizedTargetId, normalizedDesiredRevision, normalizedCurrentRevision);
            existing.Selected = true;
        }
        else
        {
            _targets.Add(new DeployFleetTarget(normalizedTargetId, normalizedDesiredRevision, normalizedCurrentRevision));
        }

        LastError = null;
        Notify();
        return true;
    }

    public void RemoveTarget(DeployFleetTarget target)
    {
        _targets.Remove(target);
        if (_targets.Count == 0)
        {
            _targets.Add(new DeployFleetTarget("honua-server", "latest"));
        }

        LastError = null;
        Notify();
    }

    public void SetAllSelected(bool selected)
    {
        foreach (var target in _targets)
        {
            target.Selected = selected;
        }

        Notify();
    }

    public bool ApplyRealtimeOperation(DeployOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.OperationId))
        {
            return false;
        }

        var target = _targets.FirstOrDefault(item =>
            string.Equals(item.Operation?.OperationId, operation.OperationId, StringComparison.OrdinalIgnoreCase));

        var operationTarget = operation.Target;
        if (target is null && !string.IsNullOrWhiteSpace(operationTarget?.TargetId))
        {
            target = _targets.FirstOrDefault(item =>
                string.Equals(item.TargetId, operationTarget.TargetId, StringComparison.OrdinalIgnoreCase));
        }

        if (target is null && operationTarget is not null && !string.IsNullOrWhiteSpace(operationTarget.TargetId))
        {
            target = new DeployFleetTarget(
                operationTarget.TargetId,
                operationTarget.DesiredRevision,
                operationTarget.CurrentRevision)
            {
                Selected = false
            };
            _targets.Add(target);
        }

        if (target is null)
        {
            return false;
        }

        if (operationTarget is not null)
        {
            target.SyncRealtimeTarget(operationTarget);
        }

        target.Operation = operation;
        target.LastError = null;
        target.LastAction = "Realtime operation update";
        target.LastUpdated = operation.UpdatedAt == default
            ? DateTimeOffset.UtcNow
            : operation.UpdatedAt;
        if (Status == DeployOrchestrationStatus.Error && _targets.All(item => item.LastError is null))
        {
            Status = DeployOrchestrationStatus.Idle;
            LastError = null;
        }

        Notify();
        return true;
    }

    public Task PlanTargetAsync(DeployFleetTarget target, CancellationToken cancellationToken = default)
        => RunTargetOperationAsync(
            DeployOrchestrationStatus.Planning,
            target,
            (item, token) => PlanTargetCoreAsync(item, token),
            cancellationToken);

    public Task PlanSelectedAsync(CancellationToken cancellationToken = default)
        => RunSelectedOperationAsync(
            DeployOrchestrationStatus.Planning,
            (target, token) => PlanTargetCoreAsync(target, token),
            cancellationToken);

    public Task CreateOperationAsync(
        DeployFleetTarget target,
        string? reason,
        bool submitImmediately,
        CancellationToken cancellationToken = default)
        => RunTargetOperationAsync(
            DeployOrchestrationStatus.Creating,
            target,
            (item, token) => CreateOperationCoreAsync(item, reason, submitImmediately, token),
            cancellationToken);

    public Task CreateOperationsForSelectedAsync(
        string? reason,
        bool submitImmediately,
        CancellationToken cancellationToken = default)
        => RunSelectedOperationAsync(
            DeployOrchestrationStatus.Creating,
            static target => target.CanCreateOperation,
            "Select at least one deploy target with a ready plan.",
            (target, token) => CreateOperationCoreAsync(target, reason, submitImmediately, token),
            cancellationToken);

    public Task SubmitOperationAsync(
        DeployFleetTarget target,
        string? reason,
        CancellationToken cancellationToken = default)
        => RunTargetOperationAsync(
            DeployOrchestrationStatus.Submitting,
            target,
            (item, token) => SubmitOperationCoreAsync(item, reason, token),
            cancellationToken);

    public Task SubmitSelectedAsync(string? reason, CancellationToken cancellationToken = default)
        => RunSelectedOperationAsync(
            DeployOrchestrationStatus.Submitting,
            static target => target.CanSubmit,
            "Select at least one deploy operation to submit.",
            (target, token) => SubmitOperationCoreAsync(target, reason, token),
            cancellationToken);

    public Task RefreshOperationAsync(DeployFleetTarget target, CancellationToken cancellationToken = default)
        => RunTargetOperationAsync(
            DeployOrchestrationStatus.Refreshing,
            target,
            (item, token) => RefreshOperationCoreAsync(item, token),
            cancellationToken);

    public Task RefreshSelectedAsync(CancellationToken cancellationToken = default)
        => RunSelectedOperationAsync(
            DeployOrchestrationStatus.Refreshing,
            static target => target.CanRefresh,
            "Select at least one deploy operation to refresh.",
            (target, token) => RefreshOperationCoreAsync(target, token),
            cancellationToken);

    public Task RollbackOperationAsync(
        DeployFleetTarget target,
        string? reason,
        CancellationToken cancellationToken = default)
        => RunTargetOperationAsync(
            DeployOrchestrationStatus.RollingBack,
            target,
            (item, token) => RollbackOperationCoreAsync(item, reason, token),
            cancellationToken);

    public Task RollbackSelectedAsync(string? reason, CancellationToken cancellationToken = default)
        => RunSelectedOperationAsync(
            DeployOrchestrationStatus.RollingBack,
            static target => target.CanRollback,
            "Select at least one deploy operation that supports rollback.",
            (target, token) => RollbackOperationCoreAsync(target, reason, token),
            cancellationToken);

    private async Task RunSelectedOperationAsync(
        DeployOrchestrationStatus status,
        Func<DeployFleetTarget, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
        => await RunSelectedOperationAsync(
            status,
            static _ => true,
            "Select at least one deploy target.",
            operation,
            cancellationToken).ConfigureAwait(false);

    private async Task RunSelectedOperationAsync(
        DeployOrchestrationStatus status,
        Func<DeployFleetTarget, bool> isEligible,
        string noEligibleTargetsMessage,
        Func<DeployFleetTarget, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var selected = SelectedTargets
            .Where(isEligible)
            .ToArray();
        if (selected.Length == 0)
        {
            LastError = noEligibleTargetsMessage;
            Notify();
            return;
        }

        try
        {
            SetStatus(status);
            foreach (var target in selected)
            {
                await ExecuteTargetOperationAsync(target, operation, cancellationToken).ConfigureAwait(false);
            }

            CompleteBatch();
        }
        catch (OperationCanceledException)
        {
            ResetAfterCancellation();
            throw;
        }
    }

    private async Task RunTargetOperationAsync(
        DeployOrchestrationStatus status,
        DeployFleetTarget target,
        Func<DeployFleetTarget, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            SetStatus(status);
            await ExecuteTargetOperationAsync(target, operation, cancellationToken).ConfigureAwait(false);
            CompleteBatch();
        }
        catch (OperationCanceledException)
        {
            ResetAfterCancellation();
            throw;
        }
    }

    private async Task ExecuteTargetOperationAsync(
        DeployFleetTarget target,
        Func<DeployFleetTarget, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        target.LastError = null;
        try
        {
            await operation(target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            target.LastError = ex.Message;
            target.LastAction = "Action failed";
            target.LastUpdated = DateTimeOffset.UtcNow;
        }

        Notify();
    }

    private async Task PlanTargetCoreAsync(DeployFleetTarget target, CancellationToken cancellationToken)
    {
        target.Plan = await _client.PlanDeployAsync(
            new DeployPlanRequest
            {
                TargetId = target.TargetId,
                DesiredRevision = target.DesiredRevision,
                CurrentRevision = target.CurrentRevision,
            },
            cancellationToken).ConfigureAwait(false);
        target.LastAction = "Plan generated";
        target.LastUpdated = target.Plan.GeneratedAt == default
            ? DateTimeOffset.UtcNow
            : target.Plan.GeneratedAt;
    }

    private async Task CreateOperationCoreAsync(
        DeployFleetTarget target,
        string? reason,
        bool submitImmediately,
        CancellationToken cancellationToken)
    {
        target.Operation = await _client.CreateDeployOperationAsync(
            new CreateDeployOperationRequest
            {
                TargetId = target.TargetId,
                DesiredRevision = target.DesiredRevision,
                CurrentRevision = target.CurrentRevision,
                Reason = NormalizeOptional(reason) ?? "Created from Honua admin deploy workspace",
                SubmitImmediately = submitImmediately,
            },
            cancellationToken).ConfigureAwait(false);
        target.LastAction = submitImmediately ? "Operation created and submitted" : "Operation created";
        target.LastUpdated = target.Operation.UpdatedAt == default
            ? DateTimeOffset.UtcNow
            : target.Operation.UpdatedAt;
    }

    private async Task SubmitOperationCoreAsync(
        DeployFleetTarget target,
        string? reason,
        CancellationToken cancellationToken)
    {
        var operationId = target.Operation?.OperationId;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            target.LastError = "Create an operation before submitting.";
            return;
        }

        target.Operation = await _client.SubmitDeployOperationAsync(
            operationId,
            new SubmitDeployOperationRequest { Reason = NormalizeOptional(reason) ?? "Submitted from Honua admin deploy workspace" },
            cancellationToken).ConfigureAwait(false);
        target.LastAction = "Operation submitted";
        target.LastUpdated = target.Operation.UpdatedAt == default
            ? DateTimeOffset.UtcNow
            : target.Operation.UpdatedAt;
    }

    private async Task RefreshOperationCoreAsync(DeployFleetTarget target, CancellationToken cancellationToken)
    {
        var operationId = target.Operation?.OperationId;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            target.LastError = "No operation ID is available for refresh.";
            return;
        }

        target.Operation = await _client.GetDeployOperationAsync(operationId, cancellationToken).ConfigureAwait(false);
        target.LastAction = "Operation refreshed";
        target.LastUpdated = target.Operation.UpdatedAt == default
            ? DateTimeOffset.UtcNow
            : target.Operation.UpdatedAt;
    }

    private async Task RollbackOperationCoreAsync(
        DeployFleetTarget target,
        string? reason,
        CancellationToken cancellationToken)
    {
        var operationId = target.Operation?.OperationId;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            target.LastError = "No operation ID is available for rollback.";
            return;
        }

        target.Operation = await _client.RollbackDeployOperationAsync(
            operationId,
            new RollbackDeployOperationRequest { Reason = NormalizeOptional(reason) ?? "Rollback requested from Honua admin deploy workspace" },
            cancellationToken).ConfigureAwait(false);
        target.LastAction = "Rollback requested";
        target.LastUpdated = target.Operation.UpdatedAt == default
            ? DateTimeOffset.UtcNow
            : target.Operation.UpdatedAt;
    }

    private void SetStatus(DeployOrchestrationStatus status)
    {
        Status = status;
        LastError = null;
        Notify();
    }

    private void CompleteBatch()
    {
        LastError = _targets.Any(target => target.LastError is not null)
            ? "One or more deploy target actions failed."
            : null;
        Status = LastError is null ? DeployOrchestrationStatus.Idle : DeployOrchestrationStatus.Error;
        Notify();
    }

    private void ResetAfterCancellation()
    {
        LastError = null;
        Status = DeployOrchestrationStatus.Idle;
        Notify();
    }

    private void Notify() => OnChanged?.Invoke();

    private static string? NormalizeRequired(string? value)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
