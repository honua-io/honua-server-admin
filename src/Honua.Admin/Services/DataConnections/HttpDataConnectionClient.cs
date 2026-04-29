using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// First non-stub client in the repo. Wraps the existing
/// <c>/api/v1/admin/connections</c> HTTP surface using
/// <see cref="HttpClient"/> + source-generated JSON. honua-server admin
/// endpoints wrap every payload in <see cref="ApiResponse{T}"/>; this client
/// unwraps via <c>envelope.Data</c>. Every method funnels through
/// <see cref="ExecuteRequestAsync{T}"/> so network / cancellation / JSON
/// failures land as typed <see cref="ConnectionOperationError"/> values.
/// The gRPC swap (<c>Honua.Sdk.Grpc</c>) replaces this once the audit
/// ticket lands.
/// </summary>
public sealed class HttpDataConnectionClient : IDataConnectionClient
{
    private const string BasePath = "api/v1/admin/connections";

    private readonly HttpClient _http;

    public HttpDataConnectionClient(HttpClient http)
    {
        _http = http;
    }

    public Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(CancellationToken cancellationToken = default) =>
        ExecuteRequestAsync(
            ct => _http.GetAsync(BasePath, ct),
            DataConnectionsJsonContext.Default.ApiResponseIReadOnlyListDataConnectionSummary,
            cancellationToken);

    public Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteRequestAsync(
            ct => _http.GetAsync($"{BasePath}/{id}", ct),
            DataConnectionsJsonContext.Default.ApiResponseDataConnectionDetail,
            cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> CreateAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default) =>
        ExecuteRequestAsync(
            ct => _http.PostAsJsonAsync(BasePath, request, DataConnectionsJsonContext.Default.CreateConnectionRequest, ct),
            DataConnectionsJsonContext.Default.ApiResponseDataConnectionSummary,
            cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> UpdateAsync(Guid id, UpdateConnectionRequest request, CancellationToken cancellationToken = default) =>
        ExecuteRequestAsync(
            ct => _http.PutAsJsonAsync($"{BasePath}/{id}", request, DataConnectionsJsonContext.Default.UpdateConnectionRequest, ct),
            DataConnectionsJsonContext.Default.ApiResponseDataConnectionSummary,
            cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> DisableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = false }, cancellationToken);

    public Task<ConnectionResult<DataConnectionSummary>> EnableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = true }, cancellationToken);

    public async Task<ConnectionResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.DeleteAsync($"{BasePath}/{id}", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult<bool>.Fail(
                    await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }
            return ConnectionResult<bool>.Ok(true);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<bool>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectionResult<bool>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.timeout"));
        }
    }

    public Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default) =>
        ExecuteRequestAsync(
            ct => _http.PostAsJsonAsync($"{BasePath}/test", request, DataConnectionsJsonContext.Default.CreateConnectionRequest, ct),
            DataConnectionsJsonContext.Default.ApiResponseConnectionTestOutcome,
            cancellationToken);

    public Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteRequestAsync(
            ct => _http.PostAsync($"{BasePath}/{id}/test", content: null, ct),
            DataConnectionsJsonContext.Default.ApiResponseConnectionTestOutcome,
            cancellationToken);

    private async Task<ConnectionResult<T>> ExecuteRequestAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> sendRequest,
        JsonTypeInfo<ApiResponse<T>> envelopeType,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await sendRequest(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult<T>.Fail(
                    await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var envelope = await response.Content
                .ReadFromJsonAsync(envelopeType, cancellationToken)
                .ConfigureAwait(false);

            if (envelope is null || envelope.Data is null)
            {
                return ConnectionResult<T>.Fail(
                    new ConnectionOperationError(ConnectionErrorKind.Server, "error.empty_response"));
            }

            return ConnectionResult<T>.Ok(envelope.Data);
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

    private static async Task<ConnectionOperationError> ParseProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ConnectionErrorKind.Auth,
            HttpStatusCode.NotFound => ConnectionErrorKind.NotFound,
            HttpStatusCode.Conflict => ConnectionErrorKind.Conflict,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ConnectionErrorKind.Validation,
            _ => ConnectionErrorKind.Server
        };
        var copyKey = $"error.{kind.ToString().ToLowerInvariant()}";

        // honua-server returns 4xx data-connection failures as
        // ApiResponse<object>.Failure(message), e.g. "Invalid SSL mode" or
        // "Connection is in use by services". A few paths still emit
        // application/problem+json (e.g., 5xx routed through middleware).
        // Read the body once and try both shapes so we never drop the
        // operator-actionable message on the floor.
        string? body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return new ConnectionOperationError(kind, copyKey);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new ConnectionOperationError(kind, copyKey);
        }

        var detail = TryReadProblemDetail(body) ?? TryReadEnvelopeMessage(body);
        return new ConnectionOperationError(kind, copyKey, detail);
    }

    private static string? TryReadProblemDetail(string body)
    {
        try
        {
            var problem = JsonSerializer.Deserialize(body, DataConnectionsJsonContext.Default.ProblemDetailsPayload);
            return problem?.Detail ?? problem?.Title;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadEnvelopeMessage(string body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize(body, DataConnectionsJsonContext.Default.ApiResponseObject);
            return string.IsNullOrWhiteSpace(envelope?.Message) ? null : envelope!.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
