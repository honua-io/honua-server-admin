using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// Deterministic in-memory backing for tests and dev. The HTTP path is the
/// production wire; the stub mirrors its observable behavior so the bUnit
/// E2E and the production path agree on every state transition. Per the
/// real server contract, mutating endpoints return a
/// <see cref="DataConnectionSummary"/> projected from the stored detail.
/// </summary>
public sealed class StubDataConnectionClient : IDataConnectionClient
{
    private readonly Dictionary<Guid, DataConnectionDetail> _store = new();

    /// <summary>
    /// Optional rule-injection point for tests: when set, the predicate runs
    /// against the host or the message hint and forces a non-healthy result.
    /// </summary>
    public Func<string, string?>? FailureMessageForHost { get; set; }

    public StubDataConnectionClient(IEnumerable<DataConnectionDetail>? seed = null)
    {
        if (seed is not null)
        {
            foreach (var detail in seed)
            {
                _store[detail.ConnectionId] = detail;
            }
        }
    }

    public Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _store.Values
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .Select(ToSummary)
            .ToArray();
        IReadOnlyList<DataConnectionSummary> result = snapshot;
        return Task.FromResult(ConnectionResult<IReadOnlyList<DataConnectionSummary>>.Ok(result));
    }

    public Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(id, out var detail))
        {
            return Task.FromResult(ConnectionResult<DataConnectionDetail>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.NotFound, "error.not_found")));
        }
        return Task.FromResult(ConnectionResult<DataConnectionDetail>.Ok(detail));
    }

    public Task<ConnectionResult<DataConnectionSummary>> CreateAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) && string.IsNullOrWhiteSpace(request.SecretReference))
        {
            return Task.FromResult(ConnectionResult<DataConnectionSummary>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Validation, "error.credential_required",
                    "Provide a password or a secret reference.")));
        }

        if (_store.Values.Any(c => string.Equals(c.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(ConnectionResult<DataConnectionSummary>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Conflict, "error.duplicate_name")));
        }

        var detail = new DataConnectionDetail
        {
            ConnectionId = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Host = request.Host,
            Port = request.Port,
            DatabaseName = request.DatabaseName,
            Username = request.Username,
            SslRequired = request.SslRequired,
            SslMode = request.SslMode,
            StorageType = string.IsNullOrWhiteSpace(request.SecretReference) ? "managed" : "external",
            IsActive = true,
            HealthStatus = "unknown",
            LastHealthCheck = null,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "operator",
            CredentialReference = request.SecretReference,
            EncryptionVersion = 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _store[detail.ConnectionId] = detail;
        return Task.FromResult(ConnectionResult<DataConnectionSummary>.Ok(ToSummary(detail)));
    }

    public Task<ConnectionResult<DataConnectionSummary>> UpdateAsync(Guid id, UpdateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(id, out var existing))
        {
            return Task.FromResult(ConnectionResult<DataConnectionSummary>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.NotFound, "error.not_found")));
        }

        var updated = new DataConnectionDetail
        {
            ConnectionId = existing.ConnectionId,
            Name = existing.Name,
            Description = request.Description ?? existing.Description,
            Host = request.Host ?? existing.Host,
            Port = request.Port ?? existing.Port,
            DatabaseName = request.DatabaseName ?? existing.DatabaseName,
            Username = request.Username ?? existing.Username,
            SslRequired = request.SslRequired ?? existing.SslRequired,
            SslMode = request.SslMode ?? existing.SslMode,
            StorageType = existing.StorageType,
            IsActive = request.IsActive ?? existing.IsActive,
            HealthStatus = existing.HealthStatus,
            LastHealthCheck = existing.LastHealthCheck,
            CreatedAt = existing.CreatedAt,
            CreatedBy = existing.CreatedBy,
            CredentialReference = existing.CredentialReference,
            EncryptionVersion = string.IsNullOrEmpty(request.Password) ? existing.EncryptionVersion : existing.EncryptionVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _store[id] = updated;
        return Task.FromResult(ConnectionResult<DataConnectionSummary>.Ok(ToSummary(updated)));
    }

    public Task<ConnectionResult<DataConnectionSummary>> DisableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = false }, cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> EnableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = true }, cancellationToken);

    public Task<ConnectionResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_store.Remove(id))
        {
            return Task.FromResult(ConnectionResult<bool>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.NotFound, "error.not_found")));
        }
        return Task.FromResult(ConnectionResult<bool>.Ok(true));
    }

    public Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var hint = FailureMessageForHost?.Invoke(request.Host);
        return Task.FromResult(BuildOutcome(Guid.Empty, request.Name, hint));
    }

    public Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(id, out var existing))
        {
            return Task.FromResult(ConnectionResult<ConnectionTestOutcome>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.NotFound, "error.not_found")));
        }

        var hint = FailureMessageForHost?.Invoke(existing.Host);
        var outcome = BuildOutcome(existing.ConnectionId, existing.Name, hint);

        if (outcome.IsSuccess)
        {
            _store[id] = existing with
            {
                HealthStatus = outcome.Value!.IsHealthy ? "healthy" : "unhealthy",
                LastHealthCheck = outcome.Value!.TestedAt
            };
        }

        return Task.FromResult(outcome);
    }

    private static ConnectionResult<ConnectionTestOutcome> BuildOutcome(Guid id, string name, string? failureMessage) =>
        ConnectionResult<ConnectionTestOutcome>.Ok(new ConnectionTestOutcome
        {
            ConnectionId = id,
            ConnectionName = name,
            IsHealthy = failureMessage is null,
            TestedAt = DateTimeOffset.UtcNow,
            Message = failureMessage ?? "Connection healthy."
        });

    private static DataConnectionSummary ToSummary(DataConnectionDetail detail) => new()
    {
        ConnectionId = detail.ConnectionId,
        Name = detail.Name,
        Description = detail.Description,
        Host = detail.Host,
        Port = detail.Port,
        DatabaseName = detail.DatabaseName,
        Username = detail.Username,
        SslRequired = detail.SslRequired,
        SslMode = detail.SslMode,
        StorageType = detail.StorageType,
        IsActive = detail.IsActive,
        HealthStatus = detail.HealthStatus,
        LastHealthCheck = detail.LastHealthCheck,
        CreatedAt = detail.CreatedAt,
        CreatedBy = detail.CreatedBy
    };
}
