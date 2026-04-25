using Honua.Admin;
using Honua.Admin.Services.Identity;
using Honua.Admin.Services.SpecWorkspace;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add MudBlazor services
builder.Services.AddMudServices();

// Spec workspace services (ticket #26 — S1 stub backend).
builder.Services.AddScoped<CatalogCache>();
builder.Services.AddScoped<IBrowserStorageService, BrowserStorageService>();
builder.Services.AddScoped<ISpecWorkspaceTelemetry, LoggingSpecWorkspaceTelemetry>();
builder.Services.AddScoped<ISpecWorkspaceClient, StubSpecWorkspaceClient>();
builder.Services.AddScoped<SpecWorkspaceState>();

// Identity admin services (ticket #22).
builder.Services.AddScoped<IIdentityAdminTelemetry, LoggingIdentityAdminTelemetry>();
builder.Services.AddHttpClient<IIdentityAdminClient, HttpIdentityAdminClient>(client =>
{
    var baseUrl = builder.Configuration["HonuaServer:BaseUrl"];
    client.BaseAddress = !string.IsNullOrWhiteSpace(baseUrl)
        ? new Uri(baseUrl)
        : new Uri(builder.HostEnvironment.BaseAddress);

    // X-API-Key carries server-admin credentials. Shipping it through WASM
    // configuration leaks it to every browser that loads the static app, so
    // only attach it in Development. Production deployments must front the
    // admin UI with a same-origin BFF that injects credentials server-side
    // (tracked as a follow-on; see README "Identity workspace" security note).
    var apiKey = builder.Configuration["HonuaServer:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        if (builder.HostEnvironment.IsDevelopment())
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        else
        {
            Console.Error.WriteLine(
                "[Honua.Admin] HonuaServer:ApiKey is set in a non-Development build. " +
                "Refusing to forward it from the browser. Front the admin UI with a same-origin " +
                "BFF that injects credentials server-side.");
        }
    }
});

// Dev auth scaffold — replaced once the real admin auth provider lands.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, DevAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// TODO: Add Honua SDK services
// builder.Services.AddHonuaGrpcClient(options => { ... });

await builder.Build().RunAsync();
