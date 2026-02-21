using Blazored.LocalStorage;
using LiveAgentConsole;
using LiveAgentConsole.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Get API base URL from configuration (supports environment-specific settings)
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5031/";
Console.WriteLine($"?? API Base URL: {apiBaseUrl}");

// Add Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// ====== AUTHENTICATION DISABLED FOR DEBUGGING ======
// Register authentication services
//builder.Services.AddAuthorizationCore();
//builder.Services.AddScoped<AuthService>();
//builder.Services.AddScoped<AgentAuthenticationStateProvider>();
//builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
//    sp.GetRequiredService<AgentAuthenticationStateProvider>());
builder.Services.AddScoped<SessionService>();
//builder.Services.AddTransient<AuthorizingHttpMessageHandler>();

// Register default HttpClient for authentication services (without handler to avoid circular dependency)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Register typed clients with authorization handler
builder.Services.AddHttpClient<LeadService>(client => {
    client.BaseAddress = new Uri(apiBaseUrl);
}); //.AddHttpMessageHandler<AuthorizingHttpMessageHandler>();

builder.Services.AddHttpClient<KpiService>(client => {
    client.BaseAddress = new Uri(apiBaseUrl);
}); //.AddHttpMessageHandler<AuthorizingHttpMessageHandler>();

// Register SignalR hub connection as scoped (needs AuthService)
builder.Services.AddScoped(sp =>
{
    var authService = sp.GetRequiredService<AuthService>();
    var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/leads";
    return new LeadHubConnection(hubUrl, authService);
});

// Register lead selection service
builder.Services.AddScoped<SelectedLeadService>();

await builder.Build().RunAsync();
