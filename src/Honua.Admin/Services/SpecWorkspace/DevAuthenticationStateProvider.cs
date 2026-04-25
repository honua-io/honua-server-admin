using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Services.SpecWorkspace;

/// <summary>
/// Dev / S1 no-op authentication state provider. The admin auth scaffold is still a TODO
/// in <c>Program.cs</c>; this stub keeps <see cref="Microsoft.AspNetCore.Components.Authorization.AuthorizeView"/>
/// gates additive-only so wiring a real provider later is a drop-in replacement.
/// </summary>
public sealed class DevAuthenticationStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "operator"),
            new Claim(ClaimTypes.Role, "operator")
        }, "dev-noop");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }
}
