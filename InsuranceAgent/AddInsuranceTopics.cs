using InsuranceAgent.Topics;
using InsuranceAgent.Topics.ComplianceTopic;
using InsuranceAgent.Topics.ContactHealthTopic;
using InsuranceAgent.Topics.ContactInfoTopic;
using InsuranceAgent.Topics.CoverageIntentTopic;
using InsuranceAgent.Topics.EmploymentTopic;
using InsuranceAgent.Topics.DependentsTopic;
using InsuranceAgent.Topics.HealthInfoTopic;
using InsuranceAgent.Topics.InsuranceContextTopic;
using InsuranceAgent.Topics.LeadDetailsTopic;
using InsuranceAgent.Topics.LifeGoalsTopic;
using Microsoft.Extensions.DependencyInjection;
using ConversaCore;

namespace InsuranceAgent.Topics {
    public static class InsuranceTopicsExtensions {
        public static IServiceCollection AddInsuranceTopics(this IServiceCollection services) {
            // Register loggers for custom topics
            services.AddScoped<ILogger<BeneficiaryInfoDemoTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<BeneficiaryInfoDemoTopic>());
            services.AddScoped<ILogger<CaliforniaResidentDemoTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<CaliforniaResidentDemoTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryRepeatDemoTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryRepeatDemoTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryUserDrivenTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryUserDrivenTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.ComplianceTopic.ComplianceTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.ComplianceTopic.ComplianceTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.ContactHealthTopic.ContactHealthTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.ContactHealthTopic.ContactHealthTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.ContactInfoTopic.ContactInfoTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.ContactInfoTopic.ContactInfoTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.CoverageIntentTopic.CoverageIntentTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.CoverageIntentTopic.CoverageIntentTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.EmploymentTopic.EmploymentTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.EmploymentTopic.EmploymentTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.DependentsTopic.DependentsTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.DependentsTopic.DependentsTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.HealthInfoTopic.HealthInfoTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.HealthInfoTopic.HealthInfoTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.InsuranceContextTopic.InsuranceContextTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.InsuranceContextTopic.InsuranceContextTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.LeadDetailsTopic.LeadDetailsTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.LeadDetailsTopic.LeadDetailsTopic>());
            services.AddScoped<ILogger<InsuranceAgent.Topics.LifeGoalsTopic.LifeGoalsTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InsuranceAgent.Topics.LifeGoalsTopic.LifeGoalsTopic>());
            services.AddScoped<ILogger<HandDownDemoTopic>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<HandDownDemoTopic>());

            // Register BeneficiaryInfoDemoTopic with all required dependencies
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new BeneficiaryInfoDemoTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<BeneficiaryInfoDemoTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>(),
                    sp.GetRequiredService<ILoggerFactory>()
                ));

            // Register CaliforniaResidentDemoTopic (update if it needs IConversationContext in future)
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new CaliforniaResidentDemoTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<CaliforniaResidentDemoTopic>>()
                ));

            // Register BeneficiaryRepeatDemoTopic with RepeatActivity support
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryRepeatDemoTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryRepeatDemoTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register BeneficiaryUserDrivenTopic with continuation cards
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryUserDrivenTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.BeneficiaryRepeatDemo.BeneficiaryUserDrivenTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register ComplianceTopic with TCPA/CCPA consent collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.ComplianceTopic.ComplianceTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.ComplianceTopic.ComplianceTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register ContactHealthTopic with contact and health details collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.ContactHealthTopic.ContactHealthTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.ContactHealthTopic.ContactHealthTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register ContactInfoTopic with contact information and preferences collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.ContactInfoTopic.ContactInfoTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.ContactInfoTopic.ContactInfoTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register CoverageIntentTopic with coverage preferences and intent collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.CoverageIntentTopic.CoverageIntentTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.CoverageIntentTopic.CoverageIntentTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register EmploymentTopic with employment status and income collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.EmploymentTopic.EmploymentTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.EmploymentTopic.EmploymentTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register DependentsTopic with family and dependents information collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.DependentsTopic.DependentsTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.DependentsTopic.DependentsTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register HealthInfoTopic with health and medical information collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.HealthInfoTopic.HealthInfoTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.HealthInfoTopic.HealthInfoTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register InsuranceContextTopic with insurance needs and financial information collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.InsuranceContextTopic.InsuranceContextTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.InsuranceContextTopic.InsuranceContextTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register LeadDetailsTopic with lead management and sales tracking information collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.LeadDetailsTopic.LeadDetailsTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.LeadDetailsTopic.LeadDetailsTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register LifeGoalsTopic with life insurance goals and intentions collection
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new InsuranceAgent.Topics.LifeGoalsTopic.LifeGoalsTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<InsuranceAgent.Topics.LifeGoalsTopic.LifeGoalsTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            // Register HandDownDemoTopic for testing hand-down/regain control mechanism
            services.AddScoped<ConversaCore.Topics.ITopic>(sp =>
                new HandDownDemoTopic(
                    sp.GetRequiredService<ConversaCore.TopicFlow.TopicWorkflowContext>(),
                    sp.GetRequiredService<ILogger<HandDownDemoTopic>>(),
                    sp.GetRequiredService<ConversaCore.Context.IConversationContext>()
                ));

            return services;
        }
    }
}