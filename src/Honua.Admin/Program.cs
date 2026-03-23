using Honua.Admin;
using Honua.Admin.Auth;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
});

// Auth state provider (singleton — holds credentials in memory, persists to localStorage)
builder.Services.AddSingleton<AdminAuthStateProvider>();

// Admin SDK client with dynamic base address from auth state
builder.Services.AddTransient<GlobalErrorHandler>();
builder.Services.AddTransient<AdminAuthHandler>();
builder.Services.AddHonuaAdmin(options =>
{
    // Base address and API key are set dynamically by AdminAuthHandler
    // at request time from AdminAuthStateProvider.
    options.BaseAddress = new Uri("https://placeholder.invalid");
    options.EnableRetry = true;
});

// Override the HttpClient registration to use our auth handler that reads
// the server URL dynamically from AdminAuthStateProvider.
builder.Services.AddHttpClient<IHonuaAdminClient, HonuaAdminClient>((sp, client) =>
{
    var auth = sp.GetRequiredService<AdminAuthStateProvider>();
    if (!string.IsNullOrEmpty(auth.ServerUrl))
    {
        client.BaseAddress = new Uri(auth.ServerUrl);
    }
})
.AddHttpMessageHandler<GlobalErrorHandler>()
.AddHttpMessageHandler<AdminAuthHandler>();

await builder.Build().RunAsync();
