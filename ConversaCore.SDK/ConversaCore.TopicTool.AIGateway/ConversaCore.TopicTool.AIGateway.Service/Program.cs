using ConversaCore.TopicTool.AIGateway.Service.Configuration;
using ConversaCore.TopicTool.AIGateway.Service.Orchestration;
using ConversaCore.TopicTool.AIGateway.Service.Validation;
using ConversaCore.TopicTool.AIGateway.Service.Contracts;
using ConversaCore.TopicTool.AIGateway.Service.Providers;

var builder = WebApplication.CreateBuilder(args);

// Kestrel hosting bound to localhost:5123 (no external exposure)
builder.WebHost.ConfigureKestrel(options =>
{
	options.ListenLocalhost(5123);
});

// Configuration binding and options
var gatewaySettings = builder.Configuration.GetSection("Gateway").Get<GatewaySettings>() ?? new GatewaySettings();
builder.Services.AddSingleton(gatewaySettings);

// Core services (minimal stubs for now)
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();
builder.Services.AddSingleton<ProviderFactory>();
builder.Services.AddSingleton<IJsonSchemaValidator, JsonSchemaValidator>();
builder.Services.AddSingleton<IAIOrchestrator, AIOrchestrator>();

var app = builder.Build();

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", version = "1.0.0" }));

// Topic designer endpoints (stub implementations)
app.MapPost("/topic-designer/generate", async (
	GenerateTopicRequest request,
	IAIOrchestrator orchestrator,
	CancellationToken ct) =>
{
	var response = await orchestrator.GenerateAsync(request, ct);
	return Results.Ok(response);
});

app.MapPost("/topic-designer/refine", async (
	RefineTopicRequest request,
	IAIOrchestrator orchestrator,
	CancellationToken ct) =>
{
	var response = await orchestrator.RefineAsync(request, ct);
	return Results.Ok(response);
});

app.Run();
