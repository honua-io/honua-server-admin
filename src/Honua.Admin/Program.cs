using Honua.Admin;
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

// Dev auth scaffold — replaced once the real admin auth provider lands.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, DevAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// TODO: Add Honua SDK services
// builder.Services.AddHonuaGrpcClient(options => { ... });

await builder.Build().RunAsync();