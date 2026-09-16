using ConversaCore.BlazorTemplateHost.Topics.SampleTopic;
using ConversaCore.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.BlazorTemplateHost.Configuration
{
    /// <summary>
    /// Registers domain topics through immutable ConversaCore descriptors.
    /// </summary>
    public static class ConversaCoreTopicRegistration
    {
        public const string SampleTopicId = "sample.start";

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
            // </conversacore-domain-topics>
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
