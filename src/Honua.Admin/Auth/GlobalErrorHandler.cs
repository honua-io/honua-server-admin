using System.Net;

namespace Honua.Admin.Auth;

public sealed class GlobalErrorHandler : DelegatingHandler
{
    private readonly AdminAuthStateProvider _auth;

    public GlobalErrorHandler(AdminAuthStateProvider auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("CORS", StringComparison.OrdinalIgnoreCase) ||
                                                ex.Message.Contains("TypeError", StringComparison.OrdinalIgnoreCase) ||
                                                ex.Message.Contains("Failed to fetch", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                $"Cross-origin request blocked. Ensure the server at {_auth.ServerUrl} allows requests from this admin UI.", ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _auth.LogoutAsync();
            throw new HttpRequestException("Session expired. Please log in again.");
        }

        return response;
    }
}
