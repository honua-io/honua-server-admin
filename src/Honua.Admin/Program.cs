using Honua.Admin;
using Honua.Admin.Auth;
using Honua.Admin.Configuration;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Annotations;
using Honua.Admin.Services.AppBuilder;
using Honua.Admin.Services.DataConnections;
using Honua.Admin.Services.DataConnections.Providers;
using Honua.Admin.Services.Identity;
using Honua.Admin.Services.LicenseWorkspace;
using Honua.Admin.Services.Operations;
using Honua.Admin.Services.OpenDataHub;
using Honua.Admin.Services.PrintService;
using Honua.Admin.Services.Publishing;
using Honua.Admin.Services.SpatialSql;
using Honua.Admin.Services.SpecWorkspace;
using Honua.Admin.Services.UsageAnalytics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Bind appsettings:HonuaServer for the typed admin client (BaseUrl + ApiKey).
builder.Services.Configure<HonuaAdminOptions>(builder.Configuration.GetSection(HonuaAdminOptions.SectionName));

// Add MudBlazor services
builder.Services.AddMudServices();

// Spec workspace services (ticket #26 — S1 stub backend).
builder.Services.AddScoped<CatalogCache>();
builder.Services.AddScoped<IBrowserStorageService, BrowserStorageService>();
builder.Services.AddScoped<ISpecWorkspaceTelemetry, LoggingSpecWorkspaceTelemetry>();
builder.Services.AddScoped<ISpecWorkspaceClient, StubSpecWorkspaceClient>();
builder.Services.AddScoped<SpecWorkspaceState>();

// Spatial SQL playground (ticket #1 — admin-side S1 stub; HTTP client lands once the
// server child tickets ship the /api/v1/admin/sql endpoints).
builder.Services.AddScoped<ISpatialSqlTelemetry, LoggingSpatialSqlTelemetry>();
builder.Services.AddScoped<ISpatialSqlClient, StubSpatialSqlClient>();
builder.Services.AddScoped<SpatialSqlPlaygroundState>();

// Map annotation workspace (issue #8) ships as an in-memory admin UI slice until
// saved-map and collaboration APIs are available from the server.
builder.Services.AddScoped<AnnotationWorkspaceState>();

// Service publishing workspace (issue #14) orchestrates existing admin control
// plane APIs across connection discovery, publish intent, protocol state, and
// deployment preflight.
builder.Services.AddScoped<PublishingWorkspaceState>();

// Usage analytics dashboard (issue #2) starts with an in-memory product
// analytics read model while durable server-side aggregation is defined.
builder.Services.AddScoped<IUsageAnalyticsClient, StubUsageAnalyticsClient>();
builder.Services.AddScoped<UsageAnalyticsState>();

// Print service workspace (issue #3) exposes the admin-side template, preview,
// and queue workflow while server-side render endpoints are wired separately.
builder.Services.AddScoped<IPrintServiceClient, StubPrintServiceClient>();
builder.Services.AddScoped<PrintServiceState>();

// App builder workspace (issue #5) exposes the admin-side low-code dashboard
// assembly workflow while server-side publish APIs are defined separately.
builder.Services.AddScoped<IAppBuilderClient, StubAppBuilderClient>();
builder.Services.AddScoped<AppBuilderState>();

// Open data hub workspace (issue #6) exposes the admin-side catalog,
// download, API, and embed readiness workflow while server APIs are defined.
builder.Services.AddScoped<IOpenDataHubClient, StubOpenDataHubClient>();
builder.Services.AddScoped<OpenDataHubState>();

// Admin client + auth wiring (ticket #28 — restored from PR #17 onto the post-#27 shell).
builder.Services.AddSingleton<AdminAuthStateProvider>();
builder.Services.AddTransient<AdminAuthHandler>();
builder.Services.AddTransient<GlobalErrorHandler>();
builder.Services.AddScoped<IAdminTelemetry, LoggingAdminTelemetry>();
builder.Services.AddScoped<AdminRealtimeEventBus>();
builder.Services.AddScoped<IAdminRealtimeEvents>(sp => sp.GetRequiredService<AdminRealtimeEventBus>());
builder.Services.AddScoped<IAdminRealtimeEventPublisher>(sp => sp.GetRequiredService<AdminRealtimeEventBus>());
builder.Services.AddScoped<IAdminRealtimeConnection, AdminRealtimeConnection>();
builder.Services.AddScoped<DeployOrchestrationState>();
builder.Services.AddScoped<OperationsConsoleState>();

// While the real honua-server is reachable the typed HttpClient routes through the
// auth + global-error handlers. Until then (default `appsettings.json` ships no
// BaseUrl) tests and bunit get the deterministic stub via DI.
var honuaServerSection = builder.Configuration.GetSection(HonuaAdminOptions.SectionName);
var honuaBaseUrl = honuaServerSection.GetValue<string>("BaseUrl");
if (!string.IsNullOrWhiteSpace(honuaBaseUrl))
{
    builder.Services.AddHttpClient<IHonuaAdminClient, HonuaAdminClient>(client =>
    {
        client.BaseAddress = new Uri(honuaBaseUrl, UriKind.Absolute);

        // RequestTimeoutSeconds advertised in README + appsettings is the per-request
        // ceiling. Floor at 1s so a misconfigured zero never disables the timeout.
        var timeoutSeconds = honuaServerSection.GetValue<int?>("RequestTimeoutSeconds")
            ?? HonuaAdminOptions.DefaultRequestTimeoutSeconds;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));

        // X-API-Key carries server-admin credentials. Mirrors the identity /
        // license clients: only attach in Development; production deployments
        // must front the admin UI with a same-origin BFF that injects
        // credentials server-side. Once admin#22 wires up operator login,
        // `AdminAuthHandler` will override this default with a runtime token.
        var apiKey = honuaServerSection.GetValue<string>("ApiKey");
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
    })
    .AddHttpMessageHandler<AdminAuthHandler>()
    .AddHttpMessageHandler<GlobalErrorHandler>();
}
else
{
    builder.Services.AddScoped<IHonuaAdminClient, StubHonuaAdminClient>();
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

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

// License workspace services (ticket #23). HttpLicenseWorkspaceClient is the
// default so the workspace reads + replaces the actual server license; the
// in-memory StubLicenseWorkspaceClient stays available for direct test /
// offline-preview construction (no DI registration). Mirrors the identity
// client's HonuaServer:BaseUrl + Development-only X-API-Key handling.
builder.Services.AddScoped<ILicenseWorkspaceTelemetry, LoggingLicenseWorkspaceTelemetry>();
builder.Services.AddHttpClient<ILicenseWorkspaceClient, HttpLicenseWorkspaceClient>(client =>
{
    var baseUrl = builder.Configuration["HonuaServer:BaseUrl"];
    client.BaseAddress = !string.IsNullOrWhiteSpace(baseUrl)
        ? new Uri(baseUrl)
        : new Uri(builder.HostEnvironment.BaseAddress);

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
builder.Services.AddScoped<LicenseWorkspaceState>();

// Data connections workspace services (ticket #24). HttpDataConnectionClient
// is the default; StubDataConnectionClient stays available for tests. Mirrors
// the identity / license HonuaServer:BaseUrl + Development-only X-API-Key
// handling so the workspace targets the real server, not the WASM host.
builder.Services.AddSingleton<IProviderRegistration, PostgresProviderRegistration>();
builder.Services.AddSingleton<IProviderRegistration, SqlServerStubProviderRegistration>();
builder.Services.AddSingleton<IProviderRegistry, ProviderRegistry>();
builder.Services.AddScoped<IDataConnectionTelemetry, LoggingDataConnectionTelemetry>();
builder.Services.AddHttpClient<IDataConnectionClient, HttpDataConnectionClient>(client =>
{
    var baseUrl = builder.Configuration["HonuaServer:BaseUrl"];
    client.BaseAddress = !string.IsNullOrWhiteSpace(baseUrl)
        ? new Uri(baseUrl)
        : new Uri(builder.HostEnvironment.BaseAddress);

    // X-API-Key handling mirrors the identity client: dev-only forwarding,
    // production must front the admin UI with a same-origin BFF.
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
builder.Services.AddScoped<DataConnectionsState>();

// Dev auth scaffold — replaced once the real admin auth provider (admin#22) lands.
// The AdminAuthStateProvider above is wired but inert until then.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, DevAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
