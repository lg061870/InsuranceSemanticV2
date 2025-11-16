#pragma warning disable SKEXP0010

using ConversaCore.Configuration;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.StateMachine;
using ConversaCore.SystemTopics;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using System.Diagnostics;

namespace ConversaCore;

public static class ServiceCollectionExtensions {
    private static void Log(string msg) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DI] {msg}");

    public static IServiceCollection AddConversaCore(this IServiceCollection services, IConfiguration? configuration = null) {
        Log("Starting ConversaCore registration...");

        // --------------------------------------------------------------------
        // Core registries & buses
        // --------------------------------------------------------------------
        services.AddSingleton<TopicRegistry>();
        //services.AddSingleton<ITopicEventBus>(_ => TopicEventBus.Instance);

        // --------------------------------------------------------------------
        // Semantic Kernel
        // --------------------------------------------------------------------
        services.AddSingleton<Kernel>(sp => {
            Log("Creating Kernel singleton...");
            var config = configuration ?? sp.GetRequiredService<IConfiguration>();
            var apiKey = config["OpenAI:ApiKey"];
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion("gpt-4o-mini", apiKey);
            Log("Kernel build complete.");
            return builder.Build();
        });

        services.AddOpenAIEmbeddingGenerator(
            modelId: configuration?["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small",
            apiKey: configuration?["OpenAI:ApiKey"]
        );

        // --------------------------------------------------------------------
        // ConversaCore intent & chat orchestration
        // --------------------------------------------------------------------
        services.AddSingleton<IIntentRecognitionService, IntentRecognitionService>();
        //services.AddScoped<IChatService, ChatService>();

        // Conversation context
        services.AddScoped<IConversationContext>(sp => {
            var ctx = new ConversationContext(
                Guid.NewGuid().ToString(),
                "anonymous",
                sp.GetRequiredService<ILogger<ConversationContext>>());
            Log($"Created ConversationContext ({ctx.GetHashCode()})");
            return ctx;
        });

        // --------------------------------------------------------------------
        // 🧠 Shared TopicWorkflowContext per scope
        // --------------------------------------------------------------------
        //services.AddScoped<TopicWorkflowContext>(sp => {
        //    var existing = sp.GetService<TopicWorkflowContext>();
        //    if (existing != null) {
        //        Log($"Reusing existing TopicWorkflowContext ({existing.GetHashCode()})");
        //        return existing;
        //    }

        //    var logger = sp.GetRequiredService<ILogger<TopicWorkflowContext>>();
        //    var ctx = new TopicWorkflowContext();
        //    Log($"Created new TopicWorkflowContext ({ctx.GetHashCode()})");
        //    return ctx;
        //});

        services.AddScoped<TopicWorkflowContext>(sp => {
            var logger = sp.GetRequiredService<ILogger<TopicWorkflowContext>>();
            var ctx = new TopicWorkflowContext();
            logger.LogInformation("[ConversaCore] Created TopicWorkflowContext ({Hash})", ctx.GetHashCode());
            return ctx;
        });


        // --------------------------------------------------------------------
        // Topic manager
        // --------------------------------------------------------------------
        services.AddScoped<ITopicManager>(sp => {
            Log("Constructing TopicManager...");
            var topics = sp.GetRequiredService<IEnumerable<ITopic>>();
            Log($"TopicManager: resolved {topics.Count()} topics");
            var context = sp.GetRequiredService<IConversationContext>();
            var wfContext = sp.GetRequiredService<TopicWorkflowContext>();
            var logger = sp.GetRequiredService<ILogger<TopicManager>>();
            return new TopicManager(topics, context, wfContext, logger);
        });

        // --------------------------------------------------------------------
        // Built-in system topics
        // --------------------------------------------------------------------
        void AddSystemTopic<T>(string name, Func<IServiceProvider, T> factory) where T : class, ITopic {
            Log($"Registering system topic: {name}");
            services.AddScoped<ITopic>(sp => {
                Log($" → Constructing {name}");
                var t = factory(sp);
                Log($" ← Finished constructing {name}");
                return t;
            });
        }

        AddSystemTopic("ConversationStartTopic", sp => new ConversationStartTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ConversationStartTopic>>(),
            sp.GetRequiredService<IConversationContext>()));

        AddSystemTopic("FallbackTopic", sp => new FallbackTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<FallbackTopic>>(),
            sp.GetRequiredService<Kernel>(),
            sp.GetService<IVectorDatabaseService>()));

        AddSystemTopic("OnErrorTopic", sp => new OnErrorTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<OnErrorTopic>>()));

        AddSystemTopic("EscalateTopic", sp => new EscalateTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<EscalateTopic>>()));

        AddSystemTopic("EndOfConversationTopic", sp => new EndOfConversationTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<EndOfConversationTopic>>()));

        AddSystemTopic("ResetConversationTopic", sp => new ResetConversationTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ResetConversationTopic>>()));

        AddSystemTopic("MultipleTopicsMatchedTopic", sp => new MultipleTopicsMatchedTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<MultipleTopicsMatchedTopic>>()));

        AddSystemTopic("SignInTopic", sp => new SignInTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<SignInTopic>>()));

        // --------------------------------------------------------------------
        // File & vector database services
        // --------------------------------------------------------------------
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<IVectorDatabaseService>(sp => {
            Log("Creating SqliteVectorDatabaseService...");
            return new SqliteVectorDatabaseService(
                sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
                sp.GetRequiredService<ILogger<SqliteVectorDatabaseService>>(),
                "vectorstore.db");
        });

        Log("✅ AddConversaCore completed successfully.");
        return services;
    }

    public static void ResetConversaCore(this IServiceProvider serviceProvider) {
        var topicRegistry = serviceProvider.GetRequiredService<TopicRegistry>();
        topicRegistry.Reset();
        foreach (var topic in serviceProvider.GetServices<ITopic>()) {
            if (!(topic is Core.ITerminable terminable) || !terminable.IsTerminated) topicRegistry.RegisterTopic(topic);
        }
        //if (serviceProvider.GetRequiredService<ITopicEventBus>() is Core.ITerminable bus) bus.Terminate();
        //try {
        //    var ctx = serviceProvider.GetRequiredService<IConversationContext>();
        //    if (ctx is Core.ITerminable t && !t.IsTerminated) t.Terminate();
        //    else ctx.Reset();
        //} catch { }
    }

    // --------------------------------------------------------------------
    // Topic registration helpers
    // --------------------------------------------------------------------
    public static TopicRegistry ConfigureTopics(this TopicRegistry registry, IServiceProvider sp) {
        Log("Starting ConfigureTopics() ...");
        try {
            var topics = sp.GetServices<ITopic>().ToList();
            Log($"ConfigureTopics: resolved {topics.Count} ITopic implementations");
            foreach (var topic in topics) {
                Log($"Registering topic: {topic.GetType().Name}");
                registry.RegisterTopic(topic);
            }
            Log("✅ ConfigureTopics finished normally.");
        } catch (Exception ex) {
            Log($"❌ Exception during ConfigureTopics: {ex}");
        }
        return registry;
    }

}
