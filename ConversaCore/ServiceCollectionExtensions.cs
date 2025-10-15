using ConversaCore.Configuration;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Services;
using ConversaCore.StateMachine;
using ConversaCore.Topics;
using ConversaCore.SystemTopics;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Embeddings;

#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.Memory;
#pragma warning restore SKEXP0001

namespace ConversaCore;

/// <summary>
/// Extension methods for setting up ConversaCore services in an IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds ConversaCore services and built-in system topics to the service collection.
    /// </summary>
    public static IServiceCollection AddConversaCore(this IServiceCollection services) {
        // Core registries & buses
        services.AddSingleton<TopicRegistry>();
        services.AddSingleton<ITopicEventBus>(_ => TopicEventBus.Instance);

        // Semantic Kernel intent recognition
        services.AddSingleton<IIntentRecognitionService, IntentRecognitionService>();

        // Chat service (per conversation/request)
        services.AddScoped<IChatService, ChatService>();

        // Conversation context: scoped, so each request/conversation carries its own ID
        services.AddScoped<IConversationContext>(sp =>
            new ConversationContext(
                conversationId: Guid.NewGuid().ToString(),
                userId: "anonymous",
                logger: sp.GetRequiredService<ILogger<ConversationContext>>()));

        // Workflow context: also scoped per conversation
        services.AddScoped<TopicWorkflowContext>();

        // Topic manager: must be scoped because it depends on IConversationContext & TopicWorkflowContext
        services.AddScoped<ITopicManager>(sp => {
            var topics = sp.GetRequiredService<IEnumerable<ITopic>>();
            var context = sp.GetRequiredService<IConversationContext>();
            var wfContext = sp.GetRequiredService<TopicWorkflowContext>();
            var logger = sp.GetRequiredService<ILogger<TopicManager>>();

            return new TopicManager(topics, context, wfContext, logger);
        });

        // === System topics (scoped, each gets its own logger) ===
        services.AddScoped<ITopic>(sp => new ConversationStartTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ConversationStartTopic>>(),
            sp.GetRequiredService<IConversationContext>()));

        services.AddScoped<ITopic>(sp => new FallbackTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<FallbackTopic>>()));

        services.AddScoped<ITopic>(sp => new OnErrorTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<OnErrorTopic>>()));

        services.AddScoped<ITopic>(sp => new EscalateTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<EscalateTopic>>()));

        services.AddScoped<ITopic>(sp => new EndOfConversationTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<EndOfConversationTopic>>()));

        services.AddScoped<ITopic>(sp => new ResetConversationTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ResetConversationTopic>>()));

        services.AddScoped<ITopic>(sp => new MultipleTopicsMatchedTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<MultipleTopicsMatchedTopic>>()));

        services.AddScoped<ITopic>(sp => new SignInTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<SignInTopic>>()));

        // Vector database services
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

        // This ensures DI can resolve loggers for ANY generic type including:
        // ILogger<AdaptiveCardActivity<TModel>>
        // ILogger<AdaptiveCardActivity<TCard, TModel>>

        return services;
    }

    /// <summary>
    /// Registers a custom topic with DI and the topic registry.
    /// Ensures the topic gets the scoped TopicWorkflowContext and ILogger<TTopic>.
    /// </summary>
    public static IServiceCollection AddTopic<TTopic>(this IServiceCollection services)
        where TTopic : TopicFlow.TopicFlow {
        services.AddScoped<ITopic>(sp =>
            (TTopic)ActivatorUtilities.CreateInstance(
                sp,
                typeof(TTopic),
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<TTopic>>())); // inject logger
        return services;
    }

    /// <summary>
    /// Adds a state machine for a specific topic.
    /// </summary>
    public static IServiceCollection AddStateMachine<TState>(
        this IServiceCollection services,
        TState initialState)
        where TState : struct, Enum {
        services.AddSingleton(_ => new TopicStateMachine<TState>(initialState));
        return services;
    }

    /// <summary>
    /// Configures topics in the topic registry using DI.
    /// </summary>
    public static TopicRegistry ConfigureTopics(
        this TopicRegistry registry,
        IServiceProvider serviceProvider) {
        var topics = serviceProvider.GetServices<ITopic>();
        foreach (var topic in topics) {
            registry.RegisterTopic(topic);
        }
        return registry;
    }

    /// <summary>
    /// Adds vector database services with configuration.
    /// </summary>
    public static IServiceCollection AddVectorDatabase(this IServiceCollection services, IConfiguration configuration) {
        // Bind vector database configuration
        var vectorConfig = new VectorDatabaseConfiguration();
        configuration.GetSection("VectorDatabase").Bind(vectorConfig);
        services.AddSingleton(vectorConfig);

        // Register vector database service based on provider
        switch (vectorConfig.Provider) {
            case VectorDatabaseProvider.InMemory:
                services.AddInMemoryVectorDatabase();
                break;
            case VectorDatabaseProvider.Chroma:
                services.AddChromaVectorDatabase(vectorConfig.ConnectionString ?? "http://localhost:8000");
                break;
            default:
                // Default to in-memory if configuration is invalid
                services.AddInMemoryVectorDatabase();
                break;
        }

        return services;
    }

    /// <summary>
    /// Adds in-memory vector database services.
    /// </summary>
    public static IServiceCollection AddInMemoryVectorDatabase(this IServiceCollection services) {
        // Register minimal implementations for now
#pragma warning disable SKEXP0001
        services.AddScoped<ISemanticTextMemory>(sp => null!);
#pragma warning restore SKEXP0001
        
        // Register Microsoft.Extensions.AI embedding generator placeholder
        services.AddScoped<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(sp => null!);
        
        services.AddScoped<IVectorDatabaseService, InMemoryVectorDatabaseService>();
        return services;
    }

    /// <summary>
    /// Adds Chroma vector database services.
    /// </summary>
    public static IServiceCollection AddChromaVectorDatabase(this IServiceCollection services, string endpoint) {
        // TODO: Implement Chroma integration when needed
        // For now, fallback to in-memory
        services.AddInMemoryVectorDatabase();
        return services;
    }

    // Convenience "batteries included"
    public static IServiceCollection AddConversaCoreWithTopics(this IServiceCollection services) {
        services.AddConversaCore();
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TopicRegistry>().ConfigureTopics(sp);
        return services;
    }
    
    /// <summary>
    /// Resets the core conversation components in the service provider.
    /// This includes the topic registry and conversation context.
    /// </summary>
    /// <param name="serviceProvider">The service provider containing the components to reset.</param>
    public static void ResetConversaCore(this IServiceProvider serviceProvider) {
        // Get the singleton TopicRegistry and reset it
        var topicRegistry = serviceProvider.GetRequiredService<TopicRegistry>();
        topicRegistry.Reset();
        
        // Re-register scoped topics with the registry
        var scopedTopics = serviceProvider.GetServices<ITopic>();
        foreach (var topic in scopedTopics) {
            // Only register topics that aren't terminated
            if (!(topic is Core.ITerminable terminable) || !terminable.IsTerminated) {
                topicRegistry.RegisterTopic(topic);
            }
        }
        
        // Reset the topic event bus (singleton)
        var topicEventBus = serviceProvider.GetRequiredService<ITopicEventBus>();
        if (topicEventBus is Core.ITerminable terminableEventBus) {
            terminableEventBus.Terminate();
        }
        
        // Get the current conversation context and reset it
        try {
            var conversationContext = serviceProvider.GetRequiredService<IConversationContext>();
            
            // Since IConversationContext now implements ITerminable, we can call Reset() directly
            // If for some reason it doesn't implement ITerminable, the explicit check is a safeguard
            if (conversationContext is Core.ITerminable terminableContext && !terminableContext.IsTerminated) {
                terminableContext.Terminate();
            } else {
                // Fallback for backward compatibility
                conversationContext.Reset();
            }
        }
        catch (Exception) {
            // Conversation context might not be available in the current scope
            // This is expected in some scenarios, so we can safely ignore this exception
        }
    }
}
