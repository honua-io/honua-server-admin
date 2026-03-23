using System.Net.Http.Headers;

namespace Honua.Admin.Auth;

public sealed class AdminAuthHandler : DelegatingHandler
{
    private readonly AdminAuthStateProvider _auth;

    public AdminAuthHandler(AdminAuthStateProvider auth)
    {
        _auth = auth;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
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
