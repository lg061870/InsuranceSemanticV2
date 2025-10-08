using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Services;
using ConversaCore.StateMachine;
using ConversaCore.Topics;
using ConversaCore.SystemTopics;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                userId: "anonymous"));

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
            sp.GetRequiredService<ILogger<ConversationStartTopic>>()));

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

    // Convenience "batteries included"
    public static IServiceCollection AddConversaCoreWithTopics(this IServiceCollection services) {
        services.AddConversaCore();
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TopicRegistry>().ConfigureTopics(sp);
        return services;
    }
}
