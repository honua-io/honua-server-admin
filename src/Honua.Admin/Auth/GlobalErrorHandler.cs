// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Admin.Auth;

/// <summary>
/// Translates network-layer failures into operator-readable error messages
/// and forces a logout on 401 so the credentials banner reflects reality.
/// Cherry-picked from PR #17.
/// </summary>
public sealed class GlobalErrorHandler : DelegatingHandler
{
    private readonly AdminAuthStateProvider _auth;

    public GlobalErrorHandler(AdminAuthStateProvider auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("CORS", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("TypeError", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Failed to fetch", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                $"Cross-origin request blocked. Ensure the server at {_auth.ServerUrl} allows requests from this admin UI.",
                ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _auth.LogoutAsync().ConfigureAwait(false);
            throw new HttpRequestException("Session expired. Please log in again.");
        }

        return response;
    }
}
