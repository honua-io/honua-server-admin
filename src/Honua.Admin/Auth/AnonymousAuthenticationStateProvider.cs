using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Auth;

/// <summary>
/// Default authentication provider used until external identity integration is configured.
/// </summary>
public sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(
        new ClaimsPrincipal(new ClaimsIdentity()));

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(AnonymousState);
    }
}
