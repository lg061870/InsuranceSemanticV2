using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Registration;

/// <summary>
/// Fluent, on-demand entry points for CC-103's startup registration validation
/// (<see cref="TopicRegistrationValidator"/>), matching the extension-method style
/// established by <see cref="ConversaCoreBuilderTopicExtensions"/> (CC-102).
/// </summary>
/// <remarks>
/// <para>
/// Neither overload wires validation automatically into any host's startup — per CC-103's
/// scope, this ticket delivers the validation capability itself, callable on demand. A
/// host decides when to call it (for example, immediately after building its
/// <see cref="IServiceProvider"/>, before <c>app.Run()</c>, alongside a designated start
/// topic ID and/or fallback topic ID it already knows).
/// </para>
/// <para>
/// Both overloads resolve <see cref="TopicDescriptor"/> instances the same way the
/// existing test helpers in <c>TopicRegistrationTests</c> already do
/// (<c>provider.GetServices&lt;TopicDescriptor&gt;()</c>), which never invokes
/// <see cref="TopicDescriptor.Factory"/> — descriptors are registered as plain singleton
/// instance values (<c>services.AddSingleton(descriptor)</c>), so resolving them from the
/// container is structural and side-effect free, matching target architecture section
/// 7.2's "Topic eligibility checks must be side-effect free and fast."
/// </para>
/// </remarks>
public static class ConversaCoreBuilderValidationExtensions
{
    /// <summary>
    /// Builds a temporary <see cref="IServiceProvider"/> from <paramref name="builder"/>'s
    /// <see cref="ConversaCoreBuilder.Services"/> and validates every resolved
    /// <see cref="TopicDescriptor"/> registered on it — including one added by bypassing
    /// <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>
    /// (for example, via a direct <c>services.AddSingleton(descriptor)</c> call), since
    /// this validates the underlying <see cref="IServiceCollection"/> itself rather than
    /// bookkeeping kept by the builder's registration methods.
    /// </summary>
    /// <param name="builder">The builder whose registered topics should be validated.</param>
    /// <param name="startTopicId">
    /// The topic ID designated as the conversation's start topic, if the host has one yet.
    /// Pass <see langword="null"/> (the default) to skip this check.
    /// </param>
    /// <param name="fallbackTopicId">
    /// The topic ID designated as the registered system fallback topic (target
    /// architecture section 7.2), if the host has one yet. Pass <see langword="null"/>
    /// (the default) to skip this check.
    /// </param>
    /// <returns>
    /// A <see cref="TopicRegistrationValidationResult"/> aggregating every problem found;
    /// see <see cref="TopicRegistrationValidator.Validate(IEnumerable{TopicDescriptor}, string?, string?)"/>
    /// for the exact checks performed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static TopicRegistrationValidationResult ValidateTopicRegistrations(
        this ConversaCoreBuilder builder,
        string? startTopicId = null,
        string? fallbackTopicId = null)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        using var serviceProvider = builder.Services.BuildServiceProvider();
        return serviceProvider.ValidateTopicRegistrations(startTopicId, fallbackTopicId);
    }

    /// <summary>
    /// Validates every <see cref="TopicDescriptor"/> resolvable from an already-built
    /// <paramref name="serviceProvider"/> — the natural entry point for a host that calls
    /// this after its own <c>IServiceProvider</c> already exists (for example, right after
    /// <c>WebApplicationBuilder.Build()</c>), instead of needing to keep the
    /// <see cref="ConversaCoreBuilder"/> around.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve registered <see cref="TopicDescriptor"/>s from.</param>
    /// <param name="startTopicId">
    /// The topic ID designated as the conversation's start topic, if the host has one yet.
    /// Pass <see langword="null"/> (the default) to skip this check.
    /// </param>
    /// <param name="fallbackTopicId">
    /// The topic ID designated as the registered system fallback topic (target
    /// architecture section 7.2), if the host has one yet. Pass <see langword="null"/>
    /// (the default) to skip this check.
    /// </param>
    /// <returns>
    /// A <see cref="TopicRegistrationValidationResult"/> aggregating every problem found;
    /// see <see cref="TopicRegistrationValidator.Validate(IEnumerable{TopicDescriptor}, string?, string?)"/>
    /// for the exact checks performed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
    public static TopicRegistrationValidationResult ValidateTopicRegistrations(
        this IServiceProvider serviceProvider,
        string? startTopicId = null,
        string? fallbackTopicId = null)
    {
        if (serviceProvider is null)
            throw new ArgumentNullException(nameof(serviceProvider));

        var descriptors = serviceProvider.GetServices<TopicDescriptor>();
        return TopicRegistrationValidator.Validate(descriptors, startTopicId, fallbackTopicId);
    }
}
