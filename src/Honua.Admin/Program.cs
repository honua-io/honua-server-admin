using Honua.Admin;
using Honua.Admin.Auth;
using Honua.Admin.Configuration;
using Honua.Admin.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.Configure<HonuaAdminOptions>(builder.Configuration.GetSection(HonuaAdminOptions.SectionName));

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();

builder.Services.AddScoped<IXlsFormService, XlsFormService>();
builder.Services.AddScoped<IFormDeploymentService, FormDeploymentService>();

await builder.Build().RunAsync();
