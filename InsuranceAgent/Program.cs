#pragma warning disable SKEXP0010

using ConversaCore;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.Topics;
using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Models;
using ConversaCore.Registration;
using ConversaCore.Registration.Compatibility;
using ConversaCore.Tools;
using InsuranceAgent.Configuration;
using InsuranceAgent.Extensions;
using InsuranceAgent.Mappings;
using InsuranceAgent.Repositories;
using InsuranceAgent.Services;
using InsuranceAgent.Topics;
using InsuranceAgent.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using System.Diagnostics;

internal class Program {
    private static void Main(string[] args) {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🟢 Starting InsuranceAgent (PID={Environment.ProcessId})");

        // ------------------------------------------------------------
        // BUILD BUILDER
        // ------------------------------------------------------------
        var builder = WebApplication.CreateBuilder(args);

        // Enable User Secrets BEFORE reading configuration
        builder.Configuration.AddUserSecrets<Program>(optional: true);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] ⚙️ Building services...");

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddControllers();

        

        var configuration = builder.Configuration;

        // ------------------------------------------------------------
        // LOAD OPENAI API KEY
        // Local development: .NET User Secrets
        // Production: environment variable OPENAI_API_KEY
        //
        // IConfiguration automatically combines configured providers,
        // including environment variables and User Secrets.
        // ------------------------------------------------------------
        string apiKey = configuration["OPENAI_API_KEY"]
            ?? throw new InvalidOperationException(
                "Missing OPENAI_API_KEY. Configure it using .NET User Secrets " +
                "or the OPENAI_API_KEY environment variable.");

        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🔑 OpenAI key loaded (length={apiKey.Length})");

        // ------------------------------------------------------------
        // CONVERSACORE (explicit key + explicit embedding model)
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🧠 Registering ConversaCore...");

        builder.Services.AddConversaCore(
            openAIApiKey: apiKey,
            embeddingModel: configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small"
        );

        // ------------------------------------------------------------
        // INTEGRATIONS
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🔗 Registering Integrations...");
        builder.Services.Configure<IntegrationsConfiguration>(
            configuration.GetSection("Integrations"));
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IIntegrationService, IntegrationService>();

        // ------------------------------------------------------------
        // INSURANCE TOPICS
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 💬 Registering InsuranceTopics...");
        builder.Services.AddInsuranceTopics();

        // Framework-owned runtime for the migrated InsuranceAgent start flow.
        new ConversaCoreBuilder(builder.Services)
            .AddTool<CreateLeadTool>(new ToolDescriptor(
                "insurance.lead.create", "1", "Create insurance lead",
                "Creates a lead from collected insurance qualification details.",
                typeof(CreateLeadRequest), typeof(CreateLeadResult),
                sideEffect: ToolSideEffect.Mutating))
            .AddTool<SaveLifeGoalsTool>(new ToolDescriptor(
                "insurance.profile.life-goals.save", "1", "Save life goals",
                "Persists the collected life-goals profile section.",
                typeof(SaveLifeGoalsRequest), typeof(ProfileWriteResult),
                sideEffect: ToolSideEffect.Mutating))
            .AddTopicsFromLegacyRegistrations(new[]
            {
                "ConversationStart",
                "BeneficiaryInfoDemoTopic",
                "CaliforniaResidentTopic",
                "BeneficiaryRepeatDemoTopic",
                "BeneficiaryUserDrivenTopic",
                "ComplianceTopic",
                "ContactHealthTopic",
                "ContactInfoTopic",
                "CoverageIntentTopic",
                "EmploymentTopic",
                "DependentsTopic",
                "HealthInfoTopic",
                "InsuranceContextTopic",
                "LeadDetailsTopic",
                "LifeGoalsTopic",
                "HandDownDemoTopic",
                "RadioButtonDemoTopic",
                "NewbieTopic",
                "MarketingT1Topic",
                "SemanticActivitiesDemoTopic",
                "EventTriggerDemoTopic",
                "ZapierIntegrationDemoTopic"
            })
            .AddTopic<InsuranceConversationStartTopic>(
                "insurance.conversation.start",
                options => options.DisplayName = "Insurance conversation start")
            .AddConversationRuntime("insurance.conversation.start");

        builder.Services.Configure<OpenAIConfiguration>(
            configuration.GetSection(OpenAIConfiguration.SectionName));

        // ------------------------------------------------------------
        // CORE SERVICES
        // ------------------------------------------------------------
        builder.Services.AddScoped<ISemanticKernelService, InsuranceSemanticKernelService>();
        builder.Services.AddScoped<IChatInteropService, ConversaCore.UI.Services.ChatInteropService>();
        builder.Services.AddScoped<IDocumentEmbeddingService, InsuranceDocumentEmbeddingService>();
        builder.Services.AddScoped<INavigationEventService, NavigationEventService>();

        // ------------------------------------------------------------
        // AUTOMAPPER CONFIG
        // ------------------------------------------------------------
        builder.Services.AddAutoMapper(_ => { }, typeof(MappingProfile));


        // ------------------------------------------------------------
        // EMBEDDINGS — used locally (Completely valid)
        // ------------------------------------------------------------
        builder.Services.AddOpenAIEmbeddingGenerator(
            modelId: configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small",
            apiKey: apiKey
        );

        // ------------------------------------------------------------
        // DATABASES / REPOSITORIES
        // ------------------------------------------------------------
        builder.Services.AddSingleton<IVectorDatabaseService, SqliteVectorDatabaseService>();
        builder.Services.AddSingleton<InsuranceRuleRepository>();

        // ------------------------------------------------------------
        // RULE INDEXER / STORE (sample implementations for developer testing)
        // ------------------------------------------------------------
        builder.Services.AddSingleton<ConversaCore.TopicFlow.Rules.IRuleIndexer, InsuranceAgent.Services.RuleIndexerSample>();
        builder.Services.AddSingleton<ConversaCore.TopicFlow.Rules.IRuleStore>(sp => {
            var env = sp.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            var path = System.IO.Path.Combine(env.ContentRootPath, "InsuranceAgent", "jsonrules", "TERM_INS_RULES_canonical.jsonl");
            return new InsuranceAgent.Services.RuleStoreSample(path);
        });

        // ------------------------------------------------------------
        // HTTP CLIENT FOR API CALLS
        // ------------------------------------------------------------
        string apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] 
            ?? "http://localhost:5031/"; // Default to local development
        
        builder.Services.AddHttpClient<LeadsService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        });

        // ------------------------------------------------------------
        // BUILD APP
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🏗️ Building app...");
        var app = builder.Build();

        if (!app.Environment.IsDevelopment()) {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        
        app.UseRouting();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.MapControllers();

        // ------------------------------------------------------------
        // TOPIC CONFIG
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🔍 Configuring topics...");

        try {
            using var scope = app.Services.CreateScope();
            var topicRegistry = scope.ServiceProvider.GetRequiredService<TopicRegistry>();
            topicRegistry.ConfigureTopics(scope.ServiceProvider);
            Console.WriteLine($"[{sw.ElapsedMilliseconds}ms]   ✅ Topic configuration complete");
        } catch (Exception ex) {
            Console.WriteLine($"[{sw.ElapsedMilliseconds}ms]   ❌ Topic configuration ERROR: {ex}");
        }

        // ------------------------------------------------------------
        // SQLITE HEALTH CHECK
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🗃️ SQLite health check...");

        try {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "vectorstore.db");
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            Console.WriteLine($"   ✅ SQLite ready: {dbPath}");
        } catch (Exception ex) {
            Console.WriteLine($"   ❌ SQLite init failed: {ex.Message}");
        }

        // ------------------------------------------------------------
        // STARTUP COMPLETE
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🚀 Application startup complete.");
        app.Run();
    }
}
