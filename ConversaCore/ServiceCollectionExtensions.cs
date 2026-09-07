#pragma warning disable SKEXP0010

using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Registration;
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
        RegisterConversaCoreServices(services, openAIApiKey, embeddingModel);
        return services;
    }

    /// <summary>
    /// Adds ConversaCore to the DI container, identically to
    /// <see cref="AddConversaCore(IServiceCollection, string, string)"/>, but returns a
    /// <see cref="ConversaCoreBuilder"/> wrapping the same <see cref="IServiceCollection"/>
    /// instead of the raw collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the builder-returning entry point introduced for CC-100 (see the
    /// ConversaCore transformation work breakdown, WP1, and target architecture section
    /// 1's <c>AddConversaCore(options => ...).AddTopicsFromAssemblyContaining&lt;T&gt;()...</c>
    /// example). It exists as a separate, differently named method rather than a literal
    /// C# overload of <see cref="AddConversaCore(IServiceCollection, string, string)"/>
    /// because a same-named overload with an identical parameter list
    /// (<c>string</c>, <c>string</c> with a default) cannot legally differ only by return
    /// type — the compiler rejects that as a duplicate signature (CS0111). Both methods
    /// call the same private <see cref="RegisterConversaCoreServices"/> registration logic,
    /// so they are guaranteed to register the identical set of services with identical
    /// lifetimes; only the returned wrapper type differs. The pre-existing
    /// <see cref="AddConversaCore(IServiceCollection, string, string)"/> overload is left
    /// completely unchanged so every current caller keeps compiling and behaving
    /// identically.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to register ConversaCore's base services into.</param>
    /// <param name="openAIApiKey">The OpenAI API key used for chat completion and embedding generation.</param>
    /// <param name="embeddingModel">The embedding model ID. Defaults to <c>"text-embedding-3-small"</c>.</param>
    /// <returns>A <see cref="ConversaCoreBuilder"/> wrapping <paramref name="services"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="openAIApiKey"/> is null, empty, or whitespace.</exception>
    public static ConversaCoreBuilder AddConversaCoreBuilder(
        this IServiceCollection services,
        string openAIApiKey,
        string embeddingModel = "text-embedding-3-small"
    ) {
        RegisterConversaCoreServices(services, openAIApiKey, embeddingModel);
        return new ConversaCoreBuilder(services);
    }

    /// <summary>
    /// Registers ConversaCore's base framework services (topic registry, Semantic Kernel,
    /// embedding generator, intent recognition, conversation context, topic manager,
    /// vector database, and base framework services) into <paramref name="services"/>.
    /// Shared by <see cref="AddConversaCore(IServiceCollection, string, string)"/> and
    /// <see cref="AddConversaCoreBuilder(IServiceCollection, string, string)"/> so both
    /// entry points perform exactly the same registration work with no duplicated logic
    /// and no risk of behavioral drift between them.
    /// </summary>
    private static void RegisterConversaCoreServices(
        IServiceCollection services,
        string openAIApiKey,
        string embeddingModel
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

        // ---------------------------------------
        // BASE FRAMEWORK SERVICES
        // ---------------------------------------
        services.AddScoped<ISemanticKernelService, Services.SemanticKernelService>();
        services.AddScoped<IDocumentEmbeddingService, Services.DocumentEmbeddingService>();

        Console.WriteLine("ConversaCore successfully registered.");
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
