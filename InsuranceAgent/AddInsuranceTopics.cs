using ConversaCore.Context;
using ConversaCore.SystemTopics;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using ConversaCore.Integrations.Core;
using InsuranceAgent.Repositories;
using InsuranceAgent.Topics;
using InsuranceAgent.Topics.BeneficiaryRepeatDemo;
using InsuranceAgent.Topics.Demo;
using Microsoft.SemanticKernel;
using ConversaCore.Tools;

namespace InsuranceAgent.Extensions;

public static class InsuranceTopicRegistrationExtensions {
    public static IServiceCollection AddInsuranceTopics(this IServiceCollection services) {
        // ─────────────────────────────
        // 🔹 Register Loggers for Each Topic
        // ─────────────────────────────
        void AddLogger<T>() where T : class =>
            services.AddScoped(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<T>());

        AddLogger<ZapierIntegrationDemoTopic>();


        // ─────────────────────────────
        // 🔹 Helper to register topics cleanly
        // ─────────────────────────────
        void AddTopic<TTopic>(Func<IServiceProvider, TTopic> factory)
            where TTopic : class, ITopic =>
            services.AddScoped<ITopic>(sp => factory(sp));


        // ─────────────────────────────
        // 🔹 Core Topics
        // ─────────────────────────────

        AddTopic(sp => new ConversationStartTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ConversationStartTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new BeneficiaryInfoDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<BeneficiaryInfoDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>(),
            sp.GetRequiredService<ILoggerFactory>()
        ));

        AddTopic(sp => new CaliforniaResidentTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<CaliforniaResidentTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new BeneficiaryRepeatDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<BeneficiaryRepeatDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new BeneficiaryUserDrivenTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<BeneficiaryUserDrivenTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new ComplianceTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ComplianceTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new ContactHealthTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ContactHealthTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new ContactInfoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ContactInfoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new CoverageIntentTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<CoverageIntentTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new EmploymentTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<EmploymentTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new DependentsTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<DependentsTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new HealthInfoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<HealthInfoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new InsuranceContextTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<InsuranceContextTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new LeadDetailsTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<LeadDetailsTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new LifeGoalsTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<LifeGoalsTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new HandDownDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<HandDownDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new RadioButtonDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<RadioButtonDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new NewbieTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<NewbieTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new MarketingT1Topic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<MarketingT1Topic>>(),
            sp.GetRequiredService<IConversationContext>(),
            sp.GetRequiredService<Kernel>(),
            sp.GetRequiredService<InsuranceRuleRepository>(),
            sp.GetRequiredService<IToolExecutor>(),
            sp
        ));

        AddTopic(sp => new SemanticActivitiesDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<SemanticActivitiesDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>(),
            sp.GetRequiredService<Kernel>()
        ));

        AddTopic(sp => new EventTriggerDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<EventTriggerDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));

        AddTopic(sp => new ZapierIntegrationDemoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<ZapierIntegrationDemoTopic>>(),
            sp.GetRequiredService<IConversationContext>(),
            sp.GetRequiredService<IIntegrationService>()
        ));

        return services;
    }

}
