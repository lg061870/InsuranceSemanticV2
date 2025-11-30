#pragma warning disable SKEXP0010

using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;

namespace ConversaCore;

public static class ServiceCollectionExtensions {
    private static void Log(string msg) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DI] {msg}");

    /// <summary>
    /// Adds ConversaCore to the DI container.
    /// The host application MUST provide the OpenAI API key and embedding model.
    /// </summary>
    public static IServiceCollection AddConversaCore(
        this IServiceCollection services,
        string openAIApiKey,
        string embeddingModel = "text-embedding-3-small"
    ) {
        Console.WriteLine("Starting ConversaCore registration...");

        // ---------------------------------------
        // VALIDATE INPUTS
        // ---------------------------------------
        if (string.IsNullOrWhiteSpace(openAIApiKey))
            throw new InvalidOperationException("OpenAI API key not provided to AddConversaCore().");

        // ---------------------------------------
        // TOPIC REGISTRY
        // ---------------------------------------
        services.AddSingleton<TopicRegistry>();

        // ---------------------------------------
        // SEMANTIC KERNEL
        // ---------------------------------------
        services.AddSingleton<Kernel>(sp => {
            Console.WriteLine("Creating Semantic Kernel...");

            var builder = Kernel.CreateBuilder();

            builder.AddOpenAIChatCompletion(
                modelId: "gpt-4o-mini",
                apiKey: openAIApiKey
            );

            return builder.Build();
        });

        // ---------------------------------------
        // EMBEDDING GENERATOR
        // ---------------------------------------
        services.AddOpenAIEmbeddingGenerator(
            modelId: embeddingModel,
            apiKey: openAIApiKey
        );

        // ---------------------------------------
        // SYSTEM SERVICES
        // ---------------------------------------
        services.AddSingleton<IIntentRecognitionService, IntentRecognitionService>();

        services.AddScoped<IConversationContext>(sp =>
            new ConversationContext(
                Guid.NewGuid().ToString(),
                "anonymous",
                sp.GetRequiredService<ILogger<ConversationContext>>()
            )
        );

        services.AddScoped<TopicWorkflowContext>();
        services.AddScoped<ITopicManager>(sp =>
            new TopicManager(
                sp.GetServices<ITopic>(),
                sp.GetRequiredService<IConversationContext>(),
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<TopicManager>>()
            )
        );

        // ---------------------------------------
        // DATABASE / VECTORS
        // ---------------------------------------
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<IVectorDatabaseService>(sp =>
            new SqliteVectorDatabaseService(
                sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
                sp.GetRequiredService<ILogger<SqliteVectorDatabaseService>>(),
                "vectorstore.db"
            )
        );

        Console.WriteLine("ConversaCore successfully registered.");
        return services;
    }

    public static void ResetConversaCore(this IServiceProvider serviceProvider) {
        var topicRegistry = serviceProvider.GetRequiredService<TopicRegistry>();
        topicRegistry.Reset();
        foreach (var topic in serviceProvider.GetServices<ITopic>()) {
            if (!(topic is Core.ITerminable terminable) || !terminable.IsTerminated) topicRegistry.RegisterTopic(topic);
        }
    }

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
