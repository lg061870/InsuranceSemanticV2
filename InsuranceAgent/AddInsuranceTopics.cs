using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using InsuranceAgent.Repositories;
using InsuranceAgent.Topics;
using InsuranceAgent.Topics.BeneficiaryRepeatDemo;
using InsuranceAgent.Topics.Demo;
using Microsoft.SemanticKernel;

namespace InsuranceAgent.Extensions;

public static class InsuranceTopicRegistrationExtensions {
    public static IServiceCollection AddInsuranceTopics(this IServiceCollection services) {
        // ─────────────────────────────
        // 🔹 Register Loggers for Each Topic
        // ─────────────────────────────
        void AddLogger<T>(IServiceCollection svc) where T : class
            => svc.AddScoped(_ => _.GetRequiredService<ILoggerFactory>().CreateLogger<T>());

        AddLogger<BeneficiaryInfoDemoTopic>(services);
        AddLogger<CaliforniaResidentTopic>(services);
        AddLogger<BeneficiaryRepeatDemoTopic>(services);
        AddLogger<BeneficiaryUserDrivenTopic>(services);
        AddLogger<ComplianceTopic>(services);
        AddLogger<ContactHealthTopic>(services);
        AddLogger<ContactInfoTopic>(services);
        AddLogger<CoverageIntentTopic>(services);
        AddLogger<EmploymentTopic>(services);
        AddLogger<DependentsTopic>(services);
        AddLogger<HealthInfoTopic>(services);
        AddLogger<InsuranceContextTopic>(services);
        AddLogger<LeadDetailsTopic>(services);
        AddLogger<LifeGoalsTopic>(services);
        AddLogger<HandDownDemoTopic>(services);
        AddLogger<RadioButtonDemoTopic>(services);
        AddLogger<MarketingT1Topic>(services);
        AddLogger<SemanticActivitiesDemoTopic>(services);
        AddLogger<EventTriggerDemoTopic>(services);

        // ─────────────────────────────
        // 🔹 Register Core Topics
        // ─────────────────────────────

        services.AddScoped<ITopic>(sp =>
            new BeneficiaryInfoDemoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<BeneficiaryInfoDemoTopic>>(),
                sp.GetRequiredService<IConversationContext>(),
                sp.GetRequiredService<ILoggerFactory>()
            ));

        services.AddScoped<ITopic>(sp =>
            new CaliforniaResidentTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<CaliforniaResidentTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new BeneficiaryRepeatDemoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<BeneficiaryRepeatDemoTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new BeneficiaryUserDrivenTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<BeneficiaryUserDrivenTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new ComplianceTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<ComplianceTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new ContactHealthTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<ContactHealthTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new ContactInfoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<ContactInfoTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new CoverageIntentTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<CoverageIntentTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new EmploymentTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<EmploymentTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new DependentsTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<DependentsTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new HealthInfoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<HealthInfoTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new InsuranceContextTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<InsuranceContextTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new LeadDetailsTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<LeadDetailsTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new LifeGoalsTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<LifeGoalsTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new HandDownDemoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<HandDownDemoTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new RadioButtonDemoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<RadioButtonDemoTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        services.AddScoped<ITopic>(sp =>
            new MarketingT1Topic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<MarketingT1Topic>>(),
                sp.GetRequiredService<IConversationContext>(),
                sp.GetRequiredService<Kernel>(),
                sp.GetRequiredService<InsuranceRuleRepository>()
            ));

        services.AddScoped<ITopic>(sp =>
            new SemanticActivitiesDemoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<SemanticActivitiesDemoTopic>>(),
                sp.GetRequiredService<IConversationContext>(),
                sp.GetRequiredService<Kernel>()
            ));

        services.AddScoped<ITopic>(sp =>
            new EventTriggerDemoTopic(
                sp.GetRequiredService<TopicWorkflowContext>(),
                sp.GetRequiredService<ILogger<EventTriggerDemoTopic>>(),
                sp.GetRequiredService<IConversationContext>()
            ));

        return services;
    }
}
