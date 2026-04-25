using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// First non-stub client in the repo. Wraps the existing
/// <c>/api/v1/admin/connections</c> HTTP surface using
/// <see cref="HttpClient"/> + source-generated JSON. The gRPC swap
/// (<c>Honua.Sdk.Grpc</c>) replaces this once the audit ticket lands.
/// </summary>
public sealed class HttpDataConnectionClient : IDataConnectionClient
{
    private const string BasePath = "api/v1/admin/connections";

    private readonly HttpClient _http;

    public HttpDataConnectionClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(BasePath, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult<IReadOnlyList<DataConnectionSummary>>.Fail(
                    await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var payload = await response.Content
                .ReadFromJsonAsync(DataConnectionsJsonContext.Default.DataConnectionSummaryArray, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<DataConnectionSummary> result = payload ?? Array.Empty<DataConnectionSummary>();
            return ConnectionResult<IReadOnlyList<DataConnectionSummary>>.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<IReadOnlyList<DataConnectionSummary>>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
    }

    public async Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync($"{BasePath}/{id}", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult<DataConnectionDetail>.Fail(
                    await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }
            var detail = await response.Content
                .ReadFromJsonAsync(DataConnectionsJsonContext.Default.DataConnectionDetail, cancellationToken)
                .ConfigureAwait(false);
            return detail is null
                ? ConnectionResult<DataConnectionDetail>.Fail(new ConnectionOperationError(ConnectionErrorKind.Server, "error.empty_response"))
                : ConnectionResult<DataConnectionDetail>.Ok(detail);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<DataConnectionDetail>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
    }

    public async Task<ConnectionResult<DataConnectionDetail>> CreateAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                BasePath, request, DataConnectionsJsonContext.Default.CreateConnectionRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult<DataConnectionDetail>.Fail(
                    await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }
            var detail = await response.Content
                .ReadFromJsonAsync(DataConnectionsJsonContext.Default.DataConnectionDetail, cancellationToken)
                .ConfigureAwait(false);
            return detail is null
                ? ConnectionResult<DataConnectionDetail>.Fail(new ConnectionOperationError(ConnectionErrorKind.Server, "error.empty_response"))
                : ConnectionResult<DataConnectionDetail>.Ok(detail);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<DataConnectionDetail>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
    }

    public async Task<ConnectionResult<DataConnectionDetail>> UpdateAsync(Guid id, UpdateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PutAsJsonAsync(
                $"{BasePath}/{id}", request, DataConnectionsJsonContext.Default.UpdateConnectionRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult<DataConnectionDetail>.Fail(
                    await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }
            var detail = await response.Content
                .ReadFromJsonAsync(DataConnectionsJsonContext.Default.DataConnectionDetail, cancellationToken)
                .ConfigureAwait(false);
            return detail is null
                ? ConnectionResult<DataConnectionDetail>.Fail(new ConnectionOperationError(ConnectionErrorKind.Server, "error.empty_response"))
                : ConnectionResult<DataConnectionDetail>.Ok(detail);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<DataConnectionDetail>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
    }

    public Task<ConnectionResult<DataConnectionDetail>> DisableAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, new UpdateConnectionRequest { IsActive = false }, cancellationToken);

    public Task<ConnectionResult<DataConnectionDetail>> EnableAsync(Guid id, CancellationToken cancellationToken = default) =>
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
    }

    public async Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{BasePath}/test", request, DataConnectionsJsonContext.Default.CreateConnectionRequest, cancellationToken).ConfigureAwait(false);
            return await ReadTestOutcomeAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<ConnectionTestOutcome>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
    }

    public async Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsync($"{BasePath}/{id}/test", content: null, cancellationToken).ConfigureAwait(false);
            return await ReadTestOutcomeAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return ConnectionResult<ConnectionTestOutcome>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Network, "error.network", ex.Message));
        }
    }

    private static async Task<ConnectionResult<ConnectionTestOutcome>> ReadTestOutcomeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ConnectionResult<ConnectionTestOutcome>.Fail(
                await ParseProblemAsync(response, cancellationToken).ConfigureAwait(false));
        }

        var outcome = await response.Content
            .ReadFromJsonAsync(DataConnectionsJsonContext.Default.ConnectionTestOutcome, cancellationToken)
            .ConfigureAwait(false);

        return outcome is null
            ? ConnectionResult<ConnectionTestOutcome>.Fail(new ConnectionOperationError(ConnectionErrorKind.Server, "error.empty_response"))
            : ConnectionResult<ConnectionTestOutcome>.Ok(outcome);
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

        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync(DataConnectionsJsonContext.Default.ProblemDetailsPayload, cancellationToken)
                .ConfigureAwait(false);
            var detail = problem?.Detail ?? problem?.Title;
            return new ConnectionOperationError(kind, $"error.{kind.ToString().ToLowerInvariant()}", detail);
        }
        catch
        {
            return new ConnectionOperationError(kind, $"error.{kind.ToString().ToLowerInvariant()}");
        }
    }
}
