using ConversaCore.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConversaCore.Registration;

/// <summary>Opt-in routing registration for the new runtime; does not replace legacy host orchestration.</summary>
public static class ConversaCoreBuilderRoutingExtensions
{
    /// <summary>Registers a singleton descriptor catalog and scoped router. An optional registered
    /// <see cref="ITopicSemanticRanker"/> is resolved from the conversation scope. Register topics before
    /// building the provider. Options and fallback references are validated when the router is resolved.</summary>
    /// <remarks>This does not register a complete conversation runtime. CC-205 owns runner composition;
    /// CC-103 still owns aggregated startup validation. Calling this twice replaces routing options.</remarks>
    public static ConversaCoreBuilder AddTopicRouting(this ConversaCoreBuilder builder, TopicRouterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton(options ?? new TopicRouterOptions()));
        builder.Services.TryAddSingleton<ITopicCatalog>(sp => new TopicCatalog(sp.GetServices<TopicDescriptor>()));
        builder.Services.TryAddScoped<ITopicRouter>(sp => new TopicRouter(
            sp.GetRequiredService<ITopicCatalog>(), sp.GetRequiredService<TopicRouterOptions>(),
            sp.GetService<ITopicSemanticRanker>()));
        return builder;
    }

    /// <summary>Wires the implemented WP2 runtime foundation using its target lifetimes.</summary>
    /// <remarks>This registers no public <see cref="IConversationRuntime"/> implementation;
    /// facade assembly remains a later task. No topic factory is invoked during registration.</remarks>
    public static ConversaCoreBuilder AddConversationRuntimeFoundation(
        this ConversaCoreBuilder builder, TopicRouterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddTopicRouting(options);
        builder.Services.TryAddScoped<IConversationSession, ConversationSession>();
        builder.Services.TryAddScoped<IConversationOutputDispatcher, ConversationOutputDispatcher>();
        builder.Services.TryAddScoped<ILegacyTopicOutputAdapter, LegacyTopicOutputAdapter>();
        builder.Services.TryAddScoped<ITopicActivator, TopicActivator>();
        builder.Services.TryAddScoped<IWorkflowRunner, WorkflowRunner>();
        builder.Services.TryAddScoped<IConversationMessageCoordinator, ConversationMessageCoordinator>();
        return builder;
    }
}
