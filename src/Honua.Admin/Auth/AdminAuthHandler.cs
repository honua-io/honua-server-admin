// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Admin.Auth;

/// <summary>
/// Adds the operator's API key to outbound admin requests and rebases relative
/// URIs onto the configured server URL. Cherry-picked from PR #17 onto the
/// post-#27 shell; until the OIDC swap (admin#22) lands, the auth header is
/// the operator's static API key.
/// </summary>
public sealed class AdminAuthHandler : DelegatingHandler
{
    private readonly AdminAuthStateProvider _auth;

    public AdminAuthHandler(AdminAuthStateProvider auth)
    {
        _auth = auth;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_auth.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", _auth.ApiKey);
        }

        if (!string.IsNullOrEmpty(_auth.ServerUrl))
        {
            var baseUri = new Uri(_auth.ServerUrl);
            if (request.RequestUri is not null && !request.RequestUri.IsAbsoluteUri)
            {
                request.RequestUri = new Uri(baseUri, request.RequestUri);
            }
            else if (request.RequestUri is null)
            {
                request.RequestUri = baseUri;
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
