using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Registration;

/// <summary>
/// Fluent entry point returned once ConversaCore's base framework services have been
/// registered, so a domain application can continue chaining registration calls onto
/// the same <see cref="IServiceCollection"/> without losing a handle to it.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately minimal skeleton (see CC-100 in the ConversaCore
/// transformation work breakdown, WP1). Its only job today is to exist and to expose
/// <see cref="Services"/>, so that ordinary <see cref="IServiceCollection"/> extension
/// methods keep working when chained after ConversaCore registration, and so that later
/// work packages (CC-102's <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c> and
/// <c>AddTool&lt;T&gt;()</c>, CC-103's startup validation, CC-402's tool catalog
/// registration, and so on — see target architecture section 1's
/// <c>AddConversaCore(options => ...).AddTopicsFromAssemblyContaining&lt;T&gt;().AddTool&lt;T&gt;()</c>
/// example) have a stable type to add fluent extension methods to. It intentionally does
/// not yet register topics, tools, perform startup validation, or own an
/// <c>ITopicCatalog</c>/<c>IToolCatalog</c> — those are later, separately tracked tickets.
/// </para>
/// <para>
/// Instances are produced by
/// <see cref="ServiceCollectionExtensions.AddConversaCoreBuilder(IServiceCollection, string, string)"/>,
/// which performs the exact same registration work as the pre-existing
/// <see cref="ServiceCollectionExtensions.AddConversaCore(IServiceCollection, string, string)"/>
/// extension (both call the same shared, private registration logic) and differs only in
/// wrapping the resulting <see cref="IServiceCollection"/> in a
/// <see cref="ConversaCoreBuilder"/> instead of returning it directly. The pre-existing
/// <c>AddConversaCore</c> overload is unchanged and continues to be the supported entry
/// point for callers that do not need the builder.
/// </para>
/// </remarks>
public sealed class ConversaCoreBuilder
{
    /// <summary>
    /// The service collection ConversaCore's base services were registered into. Exposed
    /// so ordinary <see cref="IServiceCollection"/> extension methods (and, in later work
    /// packages, ConversaCore-specific fluent methods defined on this type) can be
    /// chained without requiring a separate reference to the original collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Creates a builder wrapping the given service collection.
    /// </summary>
    /// <param name="services">
    /// The service collection ConversaCore's base services have already been registered
    /// into. Stored as-is; this constructor performs no registration of its own.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public ConversaCoreBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers a strongly typed options configuration callback with the underlying
    /// <see cref="Services"/> collection and returns this builder for further chaining.
    /// This is a thin, genuinely trivial passthrough to
    /// <see cref="OptionsServiceCollectionExtensions.Configure{TOptions}(IServiceCollection, Action{TOptions})"/>
    /// so simple options registration does not force a caller to break out of the
    /// fluent chain; it introduces no new registration or validation behavior of its own.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <param name="configure">The delegate that configures <typeparamref name="TOptions"/>.</param>
    /// <returns>This <see cref="ConversaCoreBuilder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public ConversaCoreBuilder Configure<TOptions>(Action<TOptions> configure) where TOptions : class
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        Services.Configure(configure);
        return this;
    }
}
