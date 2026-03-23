using System.Net;
using Microsoft.AspNetCore.Components;

namespace Honua.Admin.Auth;

public sealed class GlobalErrorHandler : DelegatingHandler
{
    private readonly AdminAuthStateProvider _auth;
    private readonly NavigationManager _navigation;

    public GlobalErrorHandler(AdminAuthStateProvider auth, NavigationManager navigation)
    {
        _auth = auth;
        _navigation = navigation;
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
            _navigation.NavigateTo("/login", forceLoad: true);
            throw new HttpRequestException("Session expired. Please log in again.");
        }

        return response;
    }
}
