using System;
using System.Collections.Generic;
using System.Linq;
using ConversaCore.Registration;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Registration.Compatibility;

/// <summary>
/// Compatibility bridge (CC-105, ConversaCore transformation work breakdown, WP1) that
/// translates a host's existing <c>IEnumerable&lt;ITopic&gt;</c>-style DI registrations —
/// <c>services.AddScoped&lt;ITopic, SomeTopic&gt;()</c>,
/// <c>services.AddScoped&lt;ITopic&gt;(sp =&gt; new SomeTopic(...))</c>, or a pre-built
/// singleton instance — into <see cref="TopicDescriptor"/> registrations on a
/// <see cref="ConversaCoreBuilder"/>, so a host can keep its current registration code
/// unchanged while adopting the new builder/descriptor model. See target architecture
/// section 16 ("Compatibility strategy"), step 2: "Build a compatibility adapter that
/// translates existing activity events into the new output stream" — applied here to topic
/// registrations specifically.
/// </summary>
/// <remarks>
/// <para>
/// <b>Namespace/folder isolation.</b> Per target architecture section 16, "Compatibility
/// code must be visibly isolated under a <c>Compatibility</c> namespace/folder and must not
/// become a permanent second runtime." This type therefore lives in
/// <c>ConversaCore.Registration.Compatibility</c> (folder
/// <c>ConversaCore/Registration/Compatibility/</c>) rather than alongside CC-100 through
/// CC-104's permanent registration API in <c>ConversaCore.Registration</c>, even though it
/// extends the same <see cref="ConversaCoreBuilder"/> type. It is meant to be deleted once
/// every real host (WP5/WP7) has migrated to the direct <c>AddTopic</c>/
/// <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c> APIs in
/// <see cref="ConversaCoreBuilderTopicExtensions"/>.
/// </para>
/// <para>
/// <b>Real-world evidence this design is built against.</b> Both real hosts in this
/// repository — <c>InsuranceAgent.Extensions.InsuranceTopicRegistrationExtensions.AddInsuranceTopics</c>
/// (22 topics) and <c>InsuranceLeadsAgent.Configuration.ConversaCoreTopicRegistration.AddConversaCoreDomainTopics</c>
/// (5 topics) — register every topic exclusively as
/// <c>services.AddScoped&lt;ITopic&gt;(sp =&gt; new SomeTopic(sp.GetRequiredService(...), ...))</c>.
/// That is a <b>factory-based</b> <see cref="ServiceDescriptor"/>
/// (<see cref="ServiceDescriptor.ImplementationFactory"/> populated) in 100% of real usage
/// found. Neither host uses a type-based (<c>services.AddScoped&lt;ITopic, T&gt;()</c>,
/// <see cref="ServiceDescriptor.ImplementationType"/>) or instance-based
/// (<see cref="ServiceDescriptor.ImplementationInstance"/>) registration for topics today.
/// This bridge still supports all three shapes — a general compatibility capability
/// shouldn't silently drop a shape just because today's two hosts don't happen to use it —
/// but the factory-based path is the one that matters for an actual migration of
/// <c>AddInsuranceTopics</c> (a WP5 concern, out of scope here).
/// </para>
/// <para>
/// <b>No premature instantiation (CC-104's guarantee, extended here).</b> Every
/// <see cref="ServiceDescriptor"/> for <c>ITopic</c> is inspected using only its own
/// metadata — <see cref="ServiceDescriptor.ImplementationType"/> (a <see cref="Type"/>
/// object, never constructed to be read), <see cref="ServiceDescriptor.ImplementationFactory"/>
/// (a delegate, captured and wrapped but never invoked), or
/// <see cref="ServiceDescriptor.ImplementationInstance"/> (already resolved and sitting in
/// memory before this method ever runs, so reading its <see cref="ITopic.Name"/> is not new
/// construction). Nothing in this file calls <c>IServiceProvider.GetServices&lt;ITopic&gt;()</c>
/// or otherwise resolves the DI container to "see what's registered" — doing so would
/// construct every legacy topic just to inspect it, exactly the premature-instantiation
/// defect CC-104 (and the WP0 inventory's criticism of <c>TopicRegistry.ConfigureTopics</c>)
/// exists to prevent.
/// </para>
/// <para>
/// <b>Original legacy registrations are left in place.</b> This method does not remove the
/// scanned <c>ITopic</c> <see cref="ServiceDescriptor"/> entries from
/// <see cref="ConversaCoreBuilder.Services"/> — it only adds the equivalent
/// <see cref="TopicDescriptor"/> registrations alongside them. During the migration window
/// a host may still have other code (for example, the legacy <c>TopicRegistry.ConfigureTopics</c>)
/// resolving <c>IEnumerable&lt;ITopic&gt;</c> directly; silently removing those registrations
/// here would break that code without warning. Removing them is a host-level migration
/// decision (WP5/WP7), not this compatibility bridge's call to make unilaterally.
/// </para>
/// </remarks>
public static class ConversaCoreBuilderCompatibilityExtensions
{
    /// <summary>
    /// The prefix used for the synthetic, positional topic ID assigned to a factory-based
    /// legacy <c>ITopic</c> registration when the caller does not supply a better ID for it
    /// via <paramref name="factoryTopicIds"/> (or supplies fewer IDs than factory-based
    /// registrations found). Produces IDs of the form <c>"legacy-topic-0"</c>,
    /// <c>"legacy-topic-1"</c>, and so on.
    /// </summary>
    public const string SyntheticFactoryTopicIdPrefix = "legacy-topic-";

    /// <summary>
    /// Scans <paramref name="builder"/>'s <see cref="ConversaCoreBuilder.Services"/> for
    /// existing <see cref="ServiceDescriptor"/> entries whose <see cref="ServiceDescriptor.ServiceType"/>
    /// is <see cref="ITopic"/> — the shape produced by a host's current
    /// <c>services.AddScoped&lt;ITopic, T&gt;()</c>,
    /// <c>services.AddScoped&lt;ITopic&gt;(sp =&gt; new T(...))</c>, or
    /// <c>services.AddSingleton&lt;ITopic&gt;(instance)</c> registrations — and registers an
    /// equivalent <see cref="TopicDescriptor"/> for each one found, via
    /// <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>
    /// (so CC-102's duplicate-topic-ID check applies to every translated registration exactly
    /// as it does to any other <see cref="TopicDescriptor"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How each registration shape's topic ID is determined:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Type-based</b> (<see cref="ServiceDescriptor.ImplementationType"/> populated):
    /// defaults to <c>implementationType.FullName</c> — the same default
    /// <see cref="ConversaCoreBuilderTopicExtensions.AddTopicsFromAssemblyContaining{TAssemblyMarker}(ConversaCoreBuilder)"/>
    /// already uses, for the same reason (a stable, unique, class-independent-in-spirit-but-not-in-fact
    /// ID cannot be invented without either constructing the type or accepting the class
    /// name coupling). The topic is later constructed via
    /// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/>,
    /// exactly like the assembly-scan path.
    /// </description></item>
    /// <item><description>
    /// <b>Factory-based</b> (<see cref="ServiceDescriptor.ImplementationFactory"/>
    /// populated): there is no way to read a stable, class-independent ID (like
    /// <see cref="ITopic.Name"/>) without invoking the factory, which this method must never
    /// do. Each factory-based entry is instead assigned a positional, synthetic ID —
    /// <c>"legacy-topic-{i}"</c>, where <c>i</c> is the entry's 0-based index <i>among only
    /// the factory-based entries</i>, counted in the order they are encountered while
    /// scanning <see cref="ConversaCoreBuilder.Services"/> (i.e. the order the host originally
    /// called <c>services.AddScoped&lt;ITopic&gt;(...)</c> in — type-based and
    /// instance-based entries do not consume a position in this count). A caller who already
    /// knows better IDs may supply them via <paramref name="factoryTopicIds"/>: entry <c>i</c>
    /// of that list, if present, overrides the synthetic default for the <c>i</c>-th
    /// factory-based entry found. If <paramref name="factoryTopicIds"/> has fewer entries
    /// than factory-based registrations found, every factory-based entry beyond the supplied
    /// list's length still falls back to its own <c>"legacy-topic-{i}"</c> default (using its
    /// own factory-encounter index <c>i</c>, not a renumbering as if the list were absent). If
    /// <paramref name="factoryTopicIds"/> has more entries than factory-based registrations
    /// found, the extra entries are ignored.
    /// </description></item>
    /// <item><description>
    /// <b>Instance-based</b> (<see cref="ServiceDescriptor.ImplementationInstance"/>
    /// populated): the instance already exists in memory — it was built and registered by the
    /// host before this method ever runs — so reading its real <see cref="ITopic.Name"/> here
    /// is safe (no new construction happens) and is used directly as the topic ID.
    /// </description></item>
    /// <item><description>
    /// <b>Keyed services</b> (<see cref="ServiceDescriptor.IsKeyedService"/> is
    /// <see langword="true"/>, from .NET 8's keyed-DI feature): accessing
    /// <see cref="ServiceDescriptor.ImplementationType"/>,
    /// <see cref="ServiceDescriptor.ImplementationFactory"/>, or
    /// <see cref="ServiceDescriptor.ImplementationInstance"/> on a keyed descriptor throws, so
    /// this method does not attempt it. A keyed <c>ITopic</c> registration cannot be
    /// unambiguously matched to a single conversation topic slot by this compatibility bridge
    /// (there is no keyed equivalent of <see cref="TopicDescriptor"/> or a keyed topic
    /// activation path anywhere in the target architecture yet), so it is skipped — not
    /// thrown on, and not silently dropped without a trace: a human-readable reason is
    /// appended to <paramref name="skippedRegistrations"/> when the caller supplies one. As
    /// of this writing, neither real host in this repository (<c>AddInsuranceTopics</c>,
    /// <c>AddConversaCoreDomainTopics</c>) registers a keyed <c>ITopic</c>, so this path is
    /// believed unreached in current production registrations; it exists purely as
    /// defensive, documented handling for a shape <see cref="ServiceDescriptor"/> allows.
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>Scan order.</b> Legacy entries are processed in the order they already appear in
    /// <see cref="ConversaCoreBuilder.Services"/> (i.e. the host's own registration order) —
    /// not re-sorted — because the positional factory-ID matching rule above depends on a
    /// stable, predictable encounter order a caller can reason about from their own
    /// registration code.
    /// </para>
    /// </remarks>
    /// <param name="builder">The builder whose <see cref="ConversaCoreBuilder.Services"/> should be scanned.</param>
    /// <param name="factoryTopicIds">
    /// Optional topic IDs for factory-based legacy registrations, matched positionally by
    /// factory-encounter order as documented above. Pass <see langword="null"/> (the default)
    /// to let every factory-based registration receive its synthetic
    /// <c>"legacy-topic-{i}"</c> default.
    /// </param>
    /// <param name="skippedRegistrations">
    /// Optional collection that receives one human-readable reason string for every legacy
    /// <c>ITopic</c> <see cref="ServiceDescriptor"/> found that could not be translated (for
    /// example, a keyed-service registration). Pass <see langword="null"/> (the default) to
    /// ignore skip diagnostics.
    /// </param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a translated registration's topic ID collides with an already-registered
    /// topic, via the same duplicate check every other
    /// <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>
    /// caller is subject to.
    /// </exception>
    public static ConversaCoreBuilder AddTopicsFromLegacyRegistrations(
        this ConversaCoreBuilder builder,
        IReadOnlyList<string>? factoryTopicIds = null,
        ICollection<string>? skippedRegistrations = null)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        // Snapshot first: AddTopic(descriptor) below mutates builder.Services (it adds a
        // new TopicDescriptor singleton registration for every legacy entry translated), so
        // iterating a live LINQ query over the same live IServiceCollection while mutating it
        // would throw "Collection was modified". The snapshot itself performs no
        // construction or invocation -- it only reads ServiceDescriptor metadata.
        var legacyTopicDescriptors = builder.Services
            .Where(serviceDescriptor => serviceDescriptor.ServiceType == typeof(ITopic))
            .ToList();

        var factoryIndex = 0;

        foreach (var serviceDescriptor in legacyTopicDescriptors)
        {
            if (serviceDescriptor.IsKeyedService)
            {
                skippedRegistrations?.Add(
                    $"Skipped keyed ITopic registration (service key '{serviceDescriptor.ServiceKey}'): " +
                    "AddTopicsFromLegacyRegistrations does not support keyed services -- there is no " +
                    "keyed TopicDescriptor/activation concept in the target architecture yet.");
                continue;
            }

            if (serviceDescriptor.ImplementationInstance is ITopic instance)
            {
                builder.AddTopic(new TopicDescriptor(instance.Name, _ => instance));
                continue;
            }

            if (serviceDescriptor.ImplementationType is Type implementationType)
            {
                var topicId = implementationType.FullName ?? implementationType.Name;
                builder.AddTopic(new TopicDescriptor(
                    topicId,
                    sp => (ITopic)ActivatorUtilities.CreateInstance(sp, implementationType)));
                continue;
            }

            if (serviceDescriptor.ImplementationFactory is Func<IServiceProvider, object> legacyFactory)
            {
                var topicId = factoryTopicIds is not null && factoryIndex < factoryTopicIds.Count
                    ? factoryTopicIds[factoryIndex]
                    : SyntheticFactoryTopicIdPrefix + factoryIndex;
                factoryIndex++;

                builder.AddTopic(new TopicDescriptor(
                    topicId,
                    sp => (ITopic)legacyFactory(sp)));
                continue;
            }

            // Defensive: for a non-keyed ServiceDescriptor, one of ImplementationType,
            // ImplementationFactory, or ImplementationInstance is always populated -- this
            // branch is not known to be reachable via any public ServiceDescriptor
            // construction path, but is handled the same way (skip, don't crash, record a
            // reason) rather than assumed away.
            skippedRegistrations?.Add(
                "Skipped an ITopic ServiceDescriptor with no ImplementationType, " +
                "ImplementationFactory, or ImplementationInstance set; it cannot be translated " +
                "into a TopicDescriptor.");
        }

        return builder;
    }
}
