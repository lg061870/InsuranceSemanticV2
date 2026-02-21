using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SimpleBlazorDemo.Data;
using ConversaCore;
using ConversaCore.UI.Services;
using ConversaCore.Interfaces;
using ConversaCore.SystemTopics;
using SimpleBlazorDemo.Topics;
using ConversaCore.Topics;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;
using SimpleBlazorDemo.Services;
using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Models;

var builder = WebApplication.CreateBuilder(args);

// Enable User Secrets for local development
builder.Configuration.AddUserSecrets<Program>(optional: true);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

// Add ConversaCore (framework only, no UI)
// Load OpenAI API key from environment variable OR configuration (User Secrets)
var openAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OPENAI_API_KEY"]
    ?? throw new InvalidOperationException("Missing OPENAI_API_KEY (set via User Secrets or environment variable)");

Console.WriteLine($"?? OpenAI key loaded (length={openAIApiKey.Length})");

builder.Services.AddConversaCore(
    openAIApiKey: openAIApiKey,
    embeddingModel: "text-embedding-3-small"
);

// Integrations (match InsuranceAgent integration registration pattern)
builder.Services.Configure<IntegrationsConfiguration>(
    builder.Configuration.GetSection("Integrations"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();

// Ensure Zapier integration is enabled even if configuration binding is missing or partial
builder.Services.PostConfigure<IntegrationsConfiguration>(cfg =>
{
    if (!cfg.Integrations.TryGetValue("Zapier", out var zapierConfig))
    {
        zapierConfig = new IntegrationConfiguration
        {
            Enabled = true,
            ConnectionType = "CustomerProvided"
        };
        cfg.Integrations["Zapier"] = zapierConfig;
    }
    else
    {
        zapierConfig.Enabled = true;
    }
});

// Add ConversaCore.UI services
builder.Services.AddScoped<IChatInteropService, ChatInteropService>();
builder.Services.AddScoped<ConversaCore.Agentic.DomainAgentService, SimpleBlazorDemo.Services.SimpleDomainAgentService>();
// Also register the concrete type so pages/components that request
// `SimpleDomainAgentService` directly will be resolved by DI.
builder.Services.AddScoped<SimpleBlazorDemo.Services.SimpleDomainAgentService>(sp =>
    (SimpleBlazorDemo.Services.SimpleDomainAgentService)sp.GetRequiredService<ConversaCore.Agentic.DomainAgentService>()
);

// Register educational module ingestion service
builder.Services.AddScoped<LifeInsuranceBasicsEmbeddingService>();

// Register topics
void AddLogger<T>(IServiceCollection svc) where T : class
    => svc.AddScoped(_ => _.GetRequiredService<ILoggerFactory>().CreateLogger<T>());

AddLogger<InitialpresentationTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new InitialpresentationTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<InitialpresentationTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<ILoggerFactory>()
));

AddLogger<InsuranceBasicsTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new InsuranceBasicsTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<InsuranceBasicsTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<ILoggerFactory>(),
    sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>(),
    sp.GetService<IVectorDatabaseService>()
));

AddLogger<CoverageEstimateTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new CoverageEstimateTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<CoverageEstimateTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<ILoggerFactory>(),
    sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>()
));

// Prompt-attention demo topic
AddLogger<PromptAttentionDemoTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new PromptAttentionDemoTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<PromptAttentionDemoTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<ILoggerFactory>()
));

// Zapier test topic
AddLogger<TestZapierTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new TestZapierTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<TestZapierTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<ILoggerFactory>(),
    sp.GetRequiredService<IIntegrationService>()
));

// WhatsApp test topic
AddLogger<TestWhatsAppTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new TestWhatsAppTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<TestWhatsAppTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<ILoggerFactory>(),
    sp.GetRequiredService<IIntegrationService>()
));

// Register ConversaCore system topics so the demo has the same fallback/start topics
AddLogger<ConversationStartTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new ConversationStartTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<ConversationStartTopic>>(),
    sp.GetRequiredService<IConversationContext>()
));

AddLogger<FallbackTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new FallbackTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<FallbackTopic>>(),
    sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>(),
    sp.GetService<IVectorDatabaseService>()
));

AddLogger<OnErrorTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new OnErrorTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<OnErrorTopic>>()
));

AddLogger<MultipleTopicsMatchedTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new MultipleTopicsMatchedTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<MultipleTopicsMatchedTopic>>()
));

AddLogger<SignInTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new SignInTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<SignInTopic>>()
));

AddLogger<ResetConversationTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new ResetConversationTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<ResetConversationTopic>>()
));

AddLogger<EscalateTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new EscalateTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<EscalateTopic>>()
));

AddLogger<EndOfConversationTopic>(builder.Services);
builder.Services.AddScoped<ITopic>(sp => new EndOfConversationTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<EndOfConversationTopic>>()
));

var app = builder.Build();

// ------------------------------------------------------------
// TOPIC CONFIG
// Ensure DI-registered `ITopic` implementations are registered
// with the runtime TopicRegistry (mirrors InsuranceAgent pattern).
try {
    using var scope = app.Services.CreateScope();
    var topicRegistry = scope.ServiceProvider.GetRequiredService<TopicRegistry>();
    topicRegistry.ConfigureTopics(scope.ServiceProvider);
    Console.WriteLine("Topic configuration complete");

    // Optionally sync life-insurance-basics documents into the vector store at startup.
    var lifeBasicsEmbedding = scope.ServiceProvider.GetService<LifeInsuranceBasicsEmbeddingService>();
    if (lifeBasicsEmbedding != null) {
        try {
            lifeBasicsEmbedding.SyncAsync().GetAwaiter().GetResult();
        } catch (Exception ex) {
            Console.WriteLine($"LifeInsuranceBasicsEmbeddingService sync failed: {ex}");
        }
    }
} catch (Exception ex) {
    Console.WriteLine($"Topic configuration ERROR: {ex}");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
