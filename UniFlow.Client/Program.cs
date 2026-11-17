using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using UniFlow.Client;
using UniFlow.Client.Api;
using UniFlow.Client.Applications;
using UniFlow.Client.Auth;
using UniFlow.Client.Admin;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthenticationStateProvider>());
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<ReviewListState>();
builder.Services.AddScoped<MyApplicationCounts>();
builder.Services.AddScoped<AccountListState>();
builder.Services.AddScoped<AuditLogState>();

await builder.Build().RunAsync();
