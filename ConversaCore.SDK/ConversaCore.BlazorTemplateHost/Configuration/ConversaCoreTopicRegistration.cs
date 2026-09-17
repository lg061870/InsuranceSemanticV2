using ConversaCore.BlazorTemplateHost.Tools;
using ConversaCore.BlazorTemplateHost.Topics.SampleTopic;
using ConversaCore.BlazorTemplateHost.Topics.SampleToolTopic;
using ConversaCore.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.BlazorTemplateHost.Configuration
{
    /// <summary>
    /// Registers domain topics and tools through immutable ConversaCore descriptors.
    /// </summary>
    public static class ConversaCoreTopicRegistration
    {
        public const string SampleTopicId = "sample.start";
        public const string SampleToolTopicId = "sample.tool.start";
        public const string SampleHostOutputTopicId = "sample.host.start";

        public static ConversaCoreBuilder AddConversaCoreDomainTopics(this ConversaCoreBuilder builder)
        {
            // <conversacore-domain-topics>
            builder.AddTopic<SampleTopic>(SampleTopicId, options =>
            {
                options.DisplayName = "Sample conversation";
                options.Description = "Default bounded conversation included with the template.";
                options.TriggerPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "sample topic"
                };
            });

            builder.AddTopic<SampleToolTopic>(SampleToolTopicId, options =>
            {
                options.DisplayName = "Sample tool conversation";
                options.Description = "Bounded tool execution sample demonstrating read-only and confirmed mutating tools.";
                options.TriggerPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "sample tool",
                    "tools",
                    "lookup",
                    "order"
                };
            });

            builder.AddTopic<ConversaCore.BlazorTemplateHost.Topics.SampleHostOutputTopic.SampleHostOutputTopic>(SampleHostOutputTopicId, options =>
            {
                options.DisplayName = "Sample host output conversation";
                options.Description = "Bounded host-output sample demonstrating typed notifications and correlated interactions.";
                options.TriggerPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "sample host",
                    "host output",
                    "notification",
                    "interaction",
                    "decision"
                };
            });
            // </conversacore-domain-topics>
            return builder;
        }

        public static ConversaCoreBuilder AddConversaCoreTools(this ConversaCoreBuilder builder)
        {
            // <conversacore-tools>
            builder.AddTool<SampleLookupTool>(SampleLookupTool.Descriptor);
            builder.AddTool<SampleOrderTool>(SampleOrderTool.Descriptor);
            // </conversacore-tools>
            return builder;
        }

        public static void AddConversaCoreDocumentIngestion(this IServiceCollection services)
        {
            // <conversacore-document-ingestion>
            // Document ingestion services and coordinator will be registered here
            // by the ConversaCore Topic Tool based on .conversacore.documents.json.
            // </conversacore-document-ingestion>
        }
    }
}
