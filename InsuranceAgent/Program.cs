using ConversaCore;
using ConversaCore.Services;
using ConversaCore.StateMachine;
using ConversaCore.Topics;
using InsuranceAgent.Services;
using InsuranceAgent.Configuration;
using InsuranceAgent.Topics;
using Microsoft.SemanticKernel;

internal class Program {
    private static void Main(string[] args) {
    var builder = WebApplication.CreateBuilder(args);

        // --- Logging ---
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // --- Blazor & MVC ---
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddControllers();

        // --- ConversaCore Framework + System Topics ---
        builder.Services.AddConversaCore();

    // --- InsuranceAgent-specific Topics ---
    builder.Services.AddInsuranceTopics();

        // --- Configuration ---
        builder.Services.Configure<OpenAIConfiguration>(
            builder.Configuration.GetSection(OpenAIConfiguration.SectionName));

        // --- Semantic Kernel ---
        // Configure Semantic Kernel with OpenAI and Embeddings
        builder.Services.AddSingleton<Kernel>(serviceProvider => {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var openAiConfig = new OpenAIConfiguration();
            configuration.GetSection(OpenAIConfiguration.SectionName).Bind(openAiConfig);
            
            if (!openAiConfig.IsConfigured) {
                // Create a kernel without OpenAI for development/testing
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("OpenAI API key not configured. Semantic Kernel will use fallback mode.");
                return new Kernel();
            }
            
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddOpenAIChatCompletion(openAiConfig.Model, openAiConfig.ApiKey);
            
            // Add OpenAI embeddings for vector database
            kernelBuilder.AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", openAiConfig.ApiKey);
            
            var programLogger = serviceProvider.GetRequiredService<ILogger<Program>>();
            programLogger.LogInformation("Semantic Kernel configured with OpenAI model: {Model}", openAiConfig.Model);
            
            return kernelBuilder.Build();
        });

        // --- Vector Database ---
        builder.Services.AddVectorDatabase(builder.Configuration);

        // --- Chat + Agent Services ---
        // IMPORTANT: use Scoped instead of Singleton (they depend on IConversationContext)
        builder.Services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        builder.Services.AddScoped<HybridChatService>();
        builder.Services.AddScoped<InsuranceAgentService>();
        builder.Services.AddScoped<IChatInteropService, ChatInteropService>();

        // --- Document Embedding Services ---
        builder.Services.AddScoped<IDocumentEmbeddingService, DocumentEmbeddingService>();

        // ChatService doesn’t hold per-user state → can remain singleton
    // DEBUG: Tracking Context Lifecycle - ChatService must be scoped due to IConversationContext dependency
    builder.Services.AddScoped<ChatService>();

        // --- Navigation Events ---
        // Used for cross-component navigation requests
        builder.Services.AddSingleton<INavigationEventService, NavigationEventService>();

        // --- Utilities ---
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // --- Middleware ---
        if (!app.Environment.IsDevelopment()) {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.MapControllers();

        // --- Topic Registry setup ---
        // Must resolve within a scope to avoid scoped/singleton conflicts
        using (var scope = app.Services.CreateScope()) {
            var topicRegistry = scope.ServiceProvider.GetRequiredService<TopicRegistry>();
            topicRegistry.ConfigureTopics(scope.ServiceProvider);
        }

        app.Run();
    }
}
