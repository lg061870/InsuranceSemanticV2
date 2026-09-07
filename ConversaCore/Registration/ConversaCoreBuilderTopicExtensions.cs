using System.Reflection;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Registration;

/// <summary>
/// Fluent <see cref="ConversaCoreBuilder"/> extension methods that let a domain host
/// register topics as <see cref="TopicDescriptor"/>s (CC-102, ConversaCore
/// transformation work breakdown WP1). Together with <see cref="ConversaCoreBuilder"/>
/// (CC-100) and <see cref="TopicDescriptor"/> (CC-101), these methods realize the target
/// architecture section 1 developer experience:
/// <c>AddConversaCore(options => ...).AddTopicsFromAssemblyContaining&lt;T&gt;().AddTool&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where descriptors are stored.</b> Each registered <see cref="TopicDescriptor"/> is
/// added to the underlying <see cref="IServiceCollection"/> as its own singleton instance
/// registration (<c>services.AddSingleton(descriptor)</c>) rather than accumulated into a
/// custom collection type. A consumer resolves the full registered set later via
/// constructor injection of <c>IEnumerable&lt;TopicDescriptor&gt;</c> (or
/// <c>serviceProvider.GetServices&lt;TopicDescriptor&gt;()</c>). This mirrors how ASP.NET
/// Core's own options/named-registration patterns accumulate N registrations for
/// resolution as one collection, and it deliberately does not attempt to build the actual
/// <c>ITopicCatalog</c> — that consumption-side type belongs to CC-104/CC-202, not this
/// ticket.
/// </para>
/// <para>
/// <b>Duplicate topic IDs.</b> Every registration path in this file funnels through
/// <see cref="AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>, which rejects a topic ID
/// that collides (case-insensitively, per <see cref="TopicDescriptor"/>'s equality) with
/// one already registered on the same <see cref="IServiceCollection"/> by throwing an
/// <see cref="InvalidOperationException"/> immediately, at registration time. This is a
/// single-registration-call check, not the aggregated, whole-startup validation report
/// CC-103 is responsible for; it exists only so a duplicate ID never silently overwrites
/// or shadows an earlier registration.
/// </para>
/// </remarks>
public static class ConversaCoreBuilderTopicExtensions
{
    /// <summary>
    /// Registers an already-built <see cref="TopicDescriptor"/> directly. This is the
    /// simplest registration path when a caller wants full control over descriptor
    /// construction (for example, sharing a descriptor-building helper across several
    /// registrations) instead of using the <c>AddTopic&lt;TTopic&gt;</c> convenience
    /// overloads below. Every other method in this file ultimately calls this one.
    /// </summary>
    /// <param name="builder">The builder to register the descriptor onto.</param>
    /// <param name="descriptor">The descriptor to register.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="descriptor"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a descriptor with the same <see cref="TopicDescriptor.TopicId"/>
    /// (case-insensitive) is already registered on <paramref name="builder"/>'s
    /// <see cref="ConversaCoreBuilder.Services"/>.
    /// </exception>
    public static ConversaCoreBuilder AddTopic(this ConversaCoreBuilder builder, TopicDescriptor descriptor)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));
        if (descriptor is null)
            throw new ArgumentNullException(nameof(descriptor));

        ThrowIfDuplicateTopicId(builder.Services, descriptor);
        builder.Services.AddSingleton(descriptor);
        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="TTopic"/> as a topic whose instance is resolved via
    /// ordinary constructor-injection DI activation
    /// (<see cref="ActivatorUtilities.CreateInstance{T}(IServiceProvider, object[])"/>) —
    /// the simple case where <typeparamref name="TTopic"/>'s constructor dependencies are
    /// all resolvable from the conversation's service scope, exactly as
    /// <c>services.AddScoped&lt;ITopic, TTopic&gt;()</c> would activate it, needing no
    /// hand-written factory.
    /// </summary>
    /// <typeparam name="TTopic">The concrete topic type to register.</typeparam>
    /// <param name="builder">The builder to register the topic onto.</param>
    /// <param name="topicId">
    /// The stable, unique topic identifier (see <see cref="TopicDescriptor.TopicId"/>).
    /// This is a required, explicit argument: unlike the class name, it does not default
    /// to <c>typeof(TTopic).Name</c>, because the entire point of a stable topic ID (per
    /// CC-101) is that it stays independent of and more durable than the implementing
    /// class name.
    /// </param>
    /// <param name="configure">
    /// An optional callback that sets descriptive descriptor metadata (display name,
    /// description, priority, classification, interruption policy, allowed tool IDs).
    /// When omitted, all of that metadata takes <see cref="TopicDescriptor"/>'s own
    /// documented defaults.
    /// </param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="topicId"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="topicId"/> collides with an already-registered topic,
    /// or (at factory-invocation time, later, not from this call) when
    /// <typeparamref name="TTopic"/> has no DI-resolvable constructor.
    /// </exception>
    public static ConversaCoreBuilder AddTopic<TTopic>(
        this ConversaCoreBuilder builder,
        string topicId,
        Action<TopicRegistrationOptions>? configure = null)
        where TTopic : class, ITopic
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.AddTopic(BuildDescriptor(
            topicId,
            sp => ActivatorUtilities.CreateInstance<TTopic>(sp),
            configure));
    }

    /// <summary>
    /// Registers <typeparamref name="TTopic"/> as a topic built by an explicit
    /// <paramref name="factory"/> delegate, for topics whose construction needs custom
    /// logic that ordinary DI activation cannot infer — for example, a constructor whose
    /// parameters must be resolved in a specific combination or that mixes DI-resolved
    /// services with values DI does not know about, matching how topics are constructed
    /// today by hosts such as <c>InsuranceTopicRegistrationExtensions.AddInsuranceTopics</c>
    /// (<c>services.AddScoped&lt;ITopic&gt;(sp =&gt; new SomeTopic(sp.GetRequiredService(...), ...))</c>).
    /// </summary>
    /// <typeparam name="TTopic">The concrete topic type to register.</typeparam>
    /// <param name="builder">The builder to register the topic onto.</param>
    /// <param name="topicId">
    /// The stable, unique topic identifier (see <see cref="TopicDescriptor.TopicId"/>).
    /// Required and explicit, for the same reason documented on the DI-activation
    /// overload above.
    /// </param>
    /// <param name="factory">
    /// The delegate that resolves or constructs the topic instance from a service
    /// provider scoped to one conversation. Invoked later, by a future topic activator —
    /// never invoked by this registration call.
    /// </param>
    /// <param name="configure">
    /// An optional callback that sets descriptive descriptor metadata; see the
    /// DI-activation overload for the full list of supported fields.
    /// </param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="factory"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="topicId"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="topicId"/> collides with an already-registered topic.
    /// </exception>
    public static ConversaCoreBuilder AddTopic<TTopic>(
        this ConversaCoreBuilder builder,
        string topicId,
        Func<IServiceProvider, TTopic> factory,
        Action<TopicRegistrationOptions>? configure = null)
        where TTopic : class, ITopic
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        return builder.AddTopic(BuildDescriptor(topicId, sp => factory(sp), configure));
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TAssemblyMarker"/> for every
    /// concrete, non-abstract class that implements <see cref="ITopic"/> and exposes a
    /// public constructor, and registers each as a <see cref="TopicDescriptor"/> using
    /// the same DI-activation mechanism as the single-type
    /// <see cref="AddTopic{TTopic}(ConversaCoreBuilder, string, Action{TopicRegistrationOptions}?)"/>
    /// overload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Default topic IDs.</b> A human-meaningful, stable <see cref="TopicDescriptor.TopicId"/>
    /// cannot be invented per scanned class without either violating CC-101's "independent
    /// of the implementing class name" principle or constructing every discovered type
    /// just to read an instance property — and <see cref="ITopic"/> only exposes
    /// <see cref="ITopic.Name"/> and <see cref="ITopic.Priority"/> as instance members, not
    /// static/attribute-level metadata, so there is no way to read them "for free," without
    /// activating (and thereby running the side effects of) every scanned type before
    /// registration even completes. This method therefore defaults each scanned topic's ID
    /// to <c>type.FullName</c> (the fully-qualified, namespace-qualified class name),
    /// which is guaranteed unique within one assembly and immediately gives a duplicate-ID
    /// collision if the very same type is ever also registered explicitly.
    /// </para>
    /// <para>
    /// This default ID is still coupled to the class's name and namespace, which is
    /// exactly the coupling CC-101's stable <see cref="TopicDescriptor.TopicId"/> exists to
    /// avoid for routing and subtopic references. <b>Callers who want a curated, rename-safe
    /// stable ID should use one of the <c>AddTopic&lt;TTopic&gt;</c> overloads explicitly for
    /// that topic instead of relying on this assembly-scan default.</b> A future
    /// enhancement could let a type opt into a better default (for example, a marker
    /// attribute or a static ID member read via reflection without construction); no such
    /// mechanism exists yet, so today every scanned type gets the <c>type.FullName</c>
    /// default.
    /// </para>
    /// <para>
    /// <b>Duplicate collisions.</b> Because <c>type.FullName</c> is unique per class within
    /// an assembly, a collision from the scan itself (two distinct scanned types sharing an
    /// ID) cannot happen. A collision can still occur — and is not swallowed — when a
    /// scanned type's default ID matches an ID some other registration (an explicit
    /// <c>AddTopic&lt;TTopic&gt;</c> call, a directly-registered <see cref="TopicDescriptor"/>,
    /// or an earlier assembly scan) already claimed on the same
    /// <see cref="ConversaCoreBuilder.Services"/>: registration of that particular topic
    /// throws immediately, via the same <see cref="AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>
    /// duplicate check every other registration path uses. This is a per-registration
    /// throw, not CC-103's later aggregated, whole-startup validation report.
    /// </para>
    /// </remarks>
    /// <typeparam name="TAssemblyMarker">Any type declared in the assembly to scan.</typeparam>
    /// <param name="builder">The builder to register discovered topics onto.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a scanned type's default topic ID collides with an already-registered
    /// topic.
    /// </exception>
    public static ConversaCoreBuilder AddTopicsFromAssemblyContaining<TAssemblyMarker>(this ConversaCoreBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        var assembly = typeof(TAssemblyMarker).Assembly;

        var topicTypes = GetLoadableTypes(assembly)
            .Where(IsConcreteTopicWithPublicConstructor)
            .OrderBy(type => type.FullName, StringComparer.Ordinal); // deterministic registration order

        foreach (var topicType in topicTypes)
        {
            var topicId = topicType.FullName ?? topicType.Name;
            var descriptor = new TopicDescriptor(
                topicId,
                sp => (ITopic)ActivatorUtilities.CreateInstance(sp, topicType));

            builder.AddTopic(descriptor);
        }

        return builder;
    }

    /// <summary>
    /// Builds a <see cref="TopicDescriptor"/> from the given identity/factory plus an
    /// optional metadata-configuration callback, applying <see cref="TopicRegistrationOptions"/>'s
    /// documented defaults when no callback is supplied.
    /// </summary>
    private static TopicDescriptor BuildDescriptor(
        string topicId,
        Func<IServiceProvider, ITopic> factory,
        Action<TopicRegistrationOptions>? configure)
    {
        var options = new TopicRegistrationOptions();
        configure?.Invoke(options);

        return new TopicDescriptor(topicId, factory, options.DisplayName)
        {
            Description = options.Description,
            Priority = options.Priority,
            Classification = options.Classification,
            InterruptionPolicy = options.InterruptionPolicy,
            AllowedToolIds = options.AllowedToolIds ?? new HashSet<string>()
        };
    }

    /// <summary>
    /// Throws when <paramref name="descriptor"/>'s <see cref="TopicDescriptor.TopicId"/>
    /// collides with a <see cref="TopicDescriptor"/> already registered as a singleton
    /// instance on <paramref name="services"/>. Relies on
    /// <see cref="TopicDescriptor.Equals(TopicDescriptor?)"/>'s ID-based, case-insensitive
    /// identity semantics (see CC-101).
    /// </summary>
    private static void ThrowIfDuplicateTopicId(IServiceCollection services, TopicDescriptor descriptor)
    {
        var existing = services
            .Where(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TopicDescriptor))
            .Select(serviceDescriptor => serviceDescriptor.ImplementationInstance as TopicDescriptor)
            .FirstOrDefault(registered => registered is not null && registered.Equals(descriptor));

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"A topic with ID '{descriptor.TopicId}' is already registered " +
                $"(existing display name: '{existing.DisplayName}'). Topic IDs must be " +
                "unique; choose a different topicId or remove the duplicate registration.");
        }
    }

    /// <summary>
    /// True when <paramref name="type"/> is a concrete (non-abstract, non-generic-definition)
    /// class implementing <see cref="ITopic"/> with at least one public instance
    /// constructor — the criteria <see cref="AddTopicsFromAssemblyContaining{TAssemblyMarker}(ConversaCoreBuilder)"/>
    /// uses to decide what to register.
    /// </summary>
    private static bool IsConcreteTopicWithPublicConstructor(Type type) =>
        type.IsClass
        && !type.IsAbstract
        && !type.IsGenericTypeDefinition
        && typeof(ITopic).IsAssignableFrom(type)
        && type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0;

    /// <summary>
    /// Wraps <see cref="Assembly.GetTypes"/>, tolerating an assembly that contains some
    /// types which fail to load (<see cref="ReflectionTypeLoadException"/>) by returning
    /// only the types that did load successfully instead of letting the whole scan fail.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }
}
