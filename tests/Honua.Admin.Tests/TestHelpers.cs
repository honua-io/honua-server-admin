using Bunit;
using NSubstitute;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Honua.Admin.Auth;
using Honua.Sdk.Admin;
using Microsoft.JSInterop;

namespace Honua.Admin.Tests;

public static class TestHelpers
{
    public static void ConfigureTestServices(BunitContext ctx)
    {
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();

        // MudBlazor components that use MudPopoverBase (MudSelect, MudDataGrid columns, etc.)
        // require the PopoverService to not throw during OnInitializedAsync/OnAfterRenderAsync.
        // Replace the Scoped registration with a mock that has safe defaults.
        var mockPopoverService = Substitute.For<IPopoverService>();
        mockPopoverService.CreatePopoverAsync(Arg.Any<IPopover>())
            .Returns(Task.FromResult(Guid.NewGuid()));
        mockPopoverService.UpdatePopoverAsync(Arg.Any<IPopover>())
            .Returns(Task.FromResult(true));
        mockPopoverService.DestroyPopoverAsync(Arg.Any<IPopover>())
            .Returns(Task.FromResult(true));
        mockPopoverService.GetProviderCountAsync()
            .Returns(new ValueTask<int>(1));
        ctx.Services.RemoveAll<IPopoverService>();
        ctx.Services.AddScoped<IPopoverService>(_ => mockPopoverService);

        var mockClient = Substitute.For<IHonuaAdminClient>();
        ctx.Services.AddSingleton<IHonuaAdminClient>(mockClient);

        var jsRuntime = ctx.JSInterop.JSRuntime;
        var authProvider = new AdminAuthStateProvider(jsRuntime);
        ctx.Services.AddSingleton(authProvider);
    }

    public static IHonuaAdminClient GetMockClient(BunitContext ctx)
    {
        return ctx.Services.GetRequiredService<IHonuaAdminClient>();
    }
}
