using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// One-to-one wrapper around <c>/api/v1/admin/connections</c>. Every method
/// returns a typed <see cref="ConnectionResult{T}"/> — failures never throw
/// across the boundary so the state store can fold them into a state
/// transition.
/// </summary>
public interface IDataConnectionClient
{
    Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(CancellationToken cancellationToken = default);

    Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ConnectionResult<DataConnectionDetail>> CreateAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default);

    Task<ConnectionResult<DataConnectionDetail>> UpdateAsync(Guid id, UpdateConnectionRequest request, CancellationToken cancellationToken = default);

    Task<ConnectionResult<DataConnectionDetail>> DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ConnectionResult<DataConnectionDetail>> EnableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ConnectionResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default);

    Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result envelope: either a value, or a typed error. Modeled on
/// <c>OneOf</c>-style discriminated unions without the dependency.
/// </summary>
public readonly record struct ConnectionResult<T>(T? Value, ConnectionOperationError? Error)
{
    public bool IsSuccess => Error is null;

    public static ConnectionResult<T> Ok(T value) => new(value, null);

    public static ConnectionResult<T> Fail(ConnectionOperationError error) => new(default, error);
}
