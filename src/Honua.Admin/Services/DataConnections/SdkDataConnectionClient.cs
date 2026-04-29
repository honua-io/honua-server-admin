using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;
using Honua.Sdk.Admin.Exceptions;
using SdkAdminClient = Honua.Sdk.Admin.IHonuaAdminClient;
using SdkConnectionDetail = Honua.Sdk.Admin.Models.SecureConnectionDetail;
using SdkConnectionSummary = Honua.Sdk.Admin.Models.SecureConnectionSummary;
using SdkConnectionTestResult = Honua.Sdk.Admin.Models.ConnectionTestResult;
using SdkCreateConnectionRequest = Honua.Sdk.Admin.Models.CreateSecureConnectionRequest;
using SdkUpdateConnectionRequest = Honua.Sdk.Admin.Models.UpdateSecureConnectionRequest;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// UI adapter over the reusable Honua .NET SDK admin data-connection client.
/// </summary>
public sealed class SdkDataConnectionClient : IDataConnectionClient
{
    private readonly SdkAdminClient _client;

    public SdkDataConnectionClient(SdkAdminClient client)
    {
        _client = client;
    }

    public Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            var connections = await _client.ListConnectionsAsync(ct).ConfigureAwait(false);
            var result = new List<DataConnectionSummary>(connections.Count);
            foreach (var connection in connections)
            {
                result.Add(ToSummary(connection));
            }
            return (IReadOnlyList<DataConnectionSummary>)result;
        }, cancellationToken);

    public Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            var detail = await _client.GetConnectionAsync(id.ToString("D"), ct).ConfigureAwait(false);
            return ToDetail(detail);
        }, cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> CreateAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            var created = await _client.CreateConnectionAsync(ToSdkRequest(request), ct).ConfigureAwait(false);
            return ToSummary(created);
        }, cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> UpdateAsync(Guid id, UpdateConnectionRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            var updated = await _client.UpdateConnectionAsync(id.ToString("D"), ToSdkRequest(request), ct).ConfigureAwait(false);
            return ToSummary(updated);
        }, cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> DisableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = false }, cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> EnableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = true }, cancellationToken);

    public Task<ConnectionResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            await _client.DeleteConnectionAsync(id.ToString("D"), ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            var result = await _client.TestDraftConnectionAsync(ToSdkRequest(request), ct).ConfigureAwait(false);
            return ToOutcome(result);
        }, cancellationToken);

    public Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async ct =>
        {
            var result = await _client.TestConnectionAsync(id.ToString("D"), ct).ConfigureAwait(false);
            return ToOutcome(result);
        }, cancellationToken);

    private static async Task<ConnectionResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return ConnectionResult<T>.Ok(await operation(cancellationToken).ConfigureAwait(false));
        }
        catch (HonuaAdminApiException ex)
        {
            return ConnectionResult<T>.Fail(MapApiException(ex));
        }
        catch (HonuaAdminOperationException ex)
        {
            return ConnectionResult<T>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Server, "error.empty_response", ex.Message));
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<T>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
        catch (JsonException ex)
        {
            return ConnectionResult<T>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Server, "error.malformed_response", ex.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectionResult<T>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.timeout"));
        }
    }

    private static ConnectionOperationError MapApiException(HonuaAdminApiException exception)
    {
        var kind = exception.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ConnectionErrorKind.Auth,
            HttpStatusCode.NotFound => ConnectionErrorKind.NotFound,
            HttpStatusCode.Conflict => ConnectionErrorKind.Conflict,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ConnectionErrorKind.Validation,
            _ => ConnectionErrorKind.Server
        };

        return new ConnectionOperationError(kind, CopyKey(kind), exception.Message);
    }

    private static string CopyKey(ConnectionErrorKind kind) => $"error.{kind.ToString().ToLowerInvariant()}";

    private static DataConnectionSummary ToSummary(SdkConnectionSummary connection) => new()
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
        CreatedBy = connection.CreatedBy
    };

    private static DataConnectionDetail ToDetail(SdkConnectionDetail connection) => new()
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
        UpdatedAt = connection.UpdatedAt
    };

    private static ConnectionTestOutcome ToOutcome(SdkConnectionTestResult result) => new()
    {
        ConnectionId = result.ConnectionId,
        ConnectionName = result.ConnectionName,
        IsHealthy = result.IsHealthy,
        TestedAt = result.TestedAt,
        Message = result.Message
    };

    private static SdkCreateConnectionRequest ToSdkRequest(CreateConnectionRequest request) => new()
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
        SslMode = request.SslMode
    };

    private static SdkUpdateConnectionRequest ToSdkRequest(UpdateConnectionRequest request) => new()
    {
        Description = request.Description,
        Host = request.Host,
        Port = request.Port,
        DatabaseName = request.DatabaseName,
        Username = request.Username,
        Password = request.Password,
        SslRequired = request.SslRequired,
        SslMode = request.SslMode,
        IsActive = request.IsActive
    };
}
