using ConversaCore.TopicFlow.Core;
using ConversaCore.Topics;
using ConversaCore.BlazorTemplateHost.Topics.SampleTopic;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.BlazorTemplateHost.Configuration
{
    /// <summary>
    /// Central place for registering domain-specific ConversaCore topics.
    /// Program.cs should only need to call AddConversaCoreDomainTopics.
    /// </summary>
    public static class ConversaCoreTopicRegistration
    {
        public static void AddConversaCoreDomainTopics(this IServiceCollection services)
        {
            // <conversacore-domain-topics>
            services.AddScoped<SampleTopic>();
            services.AddScoped<ITopic>(sp => sp.GetRequiredService<SampleTopic>());
            // </conversacore-domain-topics>
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
