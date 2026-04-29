// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;
using SdkAdminClient = Honua.Sdk.Admin.IHonuaAdminClient;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// UI adapter over the reusable Honua .NET SDK admin license client.
/// </summary>
public sealed class SdkLicenseWorkspaceClient : ILicenseWorkspaceClient
{
    private readonly SdkAdminClient _client;

    public SdkLicenseWorkspaceClient(SdkAdminClient client)
    {
        _client = client;
    }

    public async Task<LicenseClientResult<LicenseStatusResponse>> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _client.GetLicenseStatusAsync(cancellationToken).ConfigureAwait(false);
            return LicenseClientResult<LicenseStatusResponse>.Success(status);
        }
        catch (Exception ex) when (TryMapException(ex, cancellationToken, out var error))
        {
            return LicenseClientResult<LicenseStatusResponse>.Failure(error);
        }
    }

    public async Task<LicenseClientResult<EntitlementListBox>> GetEntitlementsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entitlements = await _client.GetLicenseEntitlementsAsync(cancellationToken).ConfigureAwait(false);
            return LicenseClientResult<EntitlementListBox>.Success(new EntitlementListBox { Items = entitlements });
        }
        catch (Exception ex) when (TryMapException(ex, cancellationToken, out var error))
        {
            return LicenseClientResult<EntitlementListBox>.Failure(error);
        }
    }

    public async Task<LicenseClientResult<LicenseStatusResponse>> UploadLicenseAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _client.UploadLicenseAsync(bytes, cancellationToken).ConfigureAwait(false);
            return LicenseClientResult<LicenseStatusResponse>.Success(status);
        }
        catch (Exception ex) when (TryMapException(ex, cancellationToken, out var error))
        {
            return LicenseClientResult<LicenseStatusResponse>.Failure(error);
        }
    }

    private static bool TryMapException(Exception exception, CancellationToken cancellationToken, out LicenseClientError error)
    {
        switch (exception)
        {
            case HonuaAdminApiException apiException:
                error = ClassifyStatus((int)apiException.StatusCode, apiException.Message);
                return true;
            case HonuaAdminOperationException operationException:
                error = new LicenseClientError(LicenseClientErrorKind.Protocol, operationException.Message);
                return true;
            case HttpRequestException httpException:
                error = new LicenseClientError(LicenseClientErrorKind.Transport, httpException.Message);
                return true;
            case OperationCanceledException when cancellationToken.IsCancellationRequested:
                error = new LicenseClientError(LicenseClientErrorKind.Transport, exception.Message);
                return false;
            case TaskCanceledException canceledException:
                error = new LicenseClientError(LicenseClientErrorKind.Transport, canceledException.Message);
                return true;
            default:
                error = new LicenseClientError(LicenseClientErrorKind.Protocol, exception.Message);
                return true;
        }
    }

    private static LicenseClientError ClassifyStatus(int statusCode, string detail)
    {
        if (statusCode is 401 or 403)
        {
            return new LicenseClientError(LicenseClientErrorKind.Authentication, detail, statusCode);
        }

        if (statusCode >= 500)
        {
            return new LicenseClientError(LicenseClientErrorKind.Server, detail, statusCode);
        }

        return new LicenseClientError(LicenseClientErrorKind.BadRequest, detail, statusCode);
    }
}
