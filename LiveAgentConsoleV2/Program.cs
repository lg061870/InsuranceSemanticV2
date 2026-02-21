using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LiveAgentConsoleV2;
using LiveAgentConsoleV2.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Get API base URL from configuration
//var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5031/api/";

//Console.WriteLine($"[STARTUP] API Base URL configured as: {apiBaseUrl}");

//// Configure HttpClient with API base address
//builder.Services.AddScoped(sp => 
//{
//    var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
//    Console.WriteLine($"[HttpClient] Created with BaseAddress: {http.BaseAddress}");
//    return http;
//});

// Register services
//builder.Services.AddScoped<SessionService>();
//builder.Services.AddScoped<KpiService>();
//builder.Services.AddScoped<LeadService>();
//builder.Services.AddScoped<SelectedLeadService>();
// SignalR hub is at /hubs/leads (not under /api prefix)
//builder.Services.AddScoped(sp => new LeadHubConnection("http://localhost:5031/hubs/leads"));

await builder.Build().RunAsync();
