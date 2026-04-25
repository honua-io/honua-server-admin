using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Calls the honua-server licensing endpoints — pinned to the working
/// <c>LicenseEndpoints</c> set (<c>GET /api/v1/admin/license</c>,
/// <c>POST /api/v1/admin/license</c>, <c>GET /api/v1/admin/license/entitlements</c>).
/// The duplicate <c>LicenseAdminEndpoints</c> set (501 upload) is intentionally
/// avoided — see the gap report.
/// </summary>
public sealed class HttpLicenseWorkspaceClient : ILicenseWorkspaceClient
{
    private const string StatusPath = "api/v1/admin/license";
    private const string EntitlementsPath = "api/v1/admin/license/entitlements";
    private const string UploadPath = "api/v1/admin/license";

    private readonly HttpClient _http;

    public HttpLicenseWorkspaceClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<LicenseClientResult<LicenseStatusDto>> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(StatusPath, cancellationToken).ConfigureAwait(false);
            return await DecodeStatusAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return LicenseClientResult<LicenseStatusDto>.Failure(TransportError(ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return LicenseClientResult<LicenseStatusDto>.Failure(TransportError(ex));
        }
    }

    public async Task<LicenseClientResult<EntitlementListBox>> GetEntitlementsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(EntitlementsPath, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return LicenseClientResult<EntitlementListBox>.Failure(ClassifyStatus(response.StatusCode, "entitlements request failed"));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var envelope = await JsonSerializer.DeserializeAsync(
                stream,
                LicenseWorkspaceJsonContext.Default.LicenseApiEnvelopeIReadOnlyListEntitlementDto,
                cancellationToken).ConfigureAwait(false);
            if (envelope is null || envelope.Data is null)
            {
                return LicenseClientResult<EntitlementListBox>.Failure(new LicenseClientError(
                    LicenseClientErrorKind.Protocol,
                    envelope?.Error ?? "entitlement response was empty"));
            }
            return LicenseClientResult<EntitlementListBox>.Success(new EntitlementListBox { Items = envelope.Data });
        }
        catch (HttpRequestException ex)
        {
            return LicenseClientResult<EntitlementListBox>.Failure(TransportError(ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return LicenseClientResult<EntitlementListBox>.Failure(TransportError(ex));
        }
        catch (JsonException ex)
        {
            return LicenseClientResult<EntitlementListBox>.Failure(new LicenseClientError(
                LicenseClientErrorKind.Protocol,
                ex.Message));
        }
    }

    public async Task<LicenseClientResult<LicenseStatusDto>> UploadLicenseAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Post, UploadPath)
            {
                Content = content
            };

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await DecodeStatusAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return LicenseClientResult<LicenseStatusDto>.Failure(TransportError(ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return LicenseClientResult<LicenseStatusDto>.Failure(TransportError(ex));
        }
    }

    private static async Task<LicenseClientResult<LicenseStatusDto>> DecodeStatusAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? bodyDetail = null;
            try
            {
                await using var errorStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                if (errorStream.CanRead)
                {
                    using var reader = new StreamReader(errorStream);
                    bodyDetail = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    if (bodyDetail.Length > 512)
                    {
                        bodyDetail = bodyDetail[..512];
                    }
                }
            }
            catch
            {
                // Body diagnostics are best-effort; the status code alone is
                // enough for the diagnostics surface to render.
            }
            return LicenseClientResult<LicenseStatusDto>.Failure(ClassifyStatus(response.StatusCode, bodyDetail ?? "license request failed"));
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var envelope = await JsonSerializer.DeserializeAsync(
                stream,
                LicenseWorkspaceJsonContext.Default.LicenseApiEnvelopeLicenseStatusDto,
                cancellationToken).ConfigureAwait(false);
            if (envelope is null || envelope.Data is null)
            {
                return LicenseClientResult<LicenseStatusDto>.Failure(new LicenseClientError(
                    LicenseClientErrorKind.Protocol,
                    envelope?.Error ?? "license status response was empty"));
            }
            return LicenseClientResult<LicenseStatusDto>.Success(envelope.Data);
        }
        catch (JsonException ex)
        {
            return LicenseClientResult<LicenseStatusDto>.Failure(new LicenseClientError(
                LicenseClientErrorKind.Protocol,
                ex.Message));
        }
    }

    private static LicenseClientError TransportError(Exception ex) =>
        new(LicenseClientErrorKind.Transport, ex.Message);

    private static LicenseClientError ClassifyStatus(HttpStatusCode statusCode, string detail)
    {
        var code = (int)statusCode;
        if (code is 401 or 403)
        {
            return new LicenseClientError(LicenseClientErrorKind.Authentication, detail, code);
        }
        if (code >= 500)
        {
            return new LicenseClientError(LicenseClientErrorKind.Server, detail, code);
        }
        return new LicenseClientError(LicenseClientErrorKind.BadRequest, detail, code);
    }
}
