using Blazored.LocalStorage;
using LiveAgentConsole;
using LiveAgentConsole.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Add Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Register authentication services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AgentAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<AgentAuthenticationStateProvider>());
builder.Services.AddScoped<SessionService>();
builder.Services.AddTransient<AuthorizingHttpMessageHandler>();

// Register default HttpClient for authentication services (without handler to avoid circular dependency)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5031/")
});

// Register typed clients with authorization handler
builder.Services.AddHttpClient<LeadService>(client => {
    client.BaseAddress = new Uri("http://localhost:5031/");
}).AddHttpMessageHandler<AuthorizingHttpMessageHandler>();

builder.Services.AddHttpClient<KpiService>(client => {
    client.BaseAddress = new Uri("http://localhost:5031/");
}).AddHttpMessageHandler<AuthorizingHttpMessageHandler>();

// Register SignalR hub connection as scoped (needs AuthService)
builder.Services.AddScoped(sp =>
{
    var authService = sp.GetRequiredService<AuthService>();
    return new LeadHubConnection("http://localhost:5031/hubs/leads", authService);
});

// Register lead selection service
builder.Services.AddScoped<SelectedLeadService>();

await builder.Build().RunAsync();
