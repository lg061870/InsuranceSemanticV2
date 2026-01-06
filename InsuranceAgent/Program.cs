#pragma warning disable SKEXP0010

using ConversaCore;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.Topics;
using InsuranceAgent.Configuration;
using InsuranceAgent.Extensions;
using InsuranceAgent.Mappings;
using InsuranceAgent.Repositories;
using InsuranceAgent.Services;
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
        // LOAD OPENAI API KEY (single source of truth)
        // ------------------------------------------------------------
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("Missing OPENAI_API_KEY");

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
        // INSURANCE TOPICS
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 💬 Registering InsuranceTopics...");
        builder.Services.AddInsuranceTopics();

        builder.Services.Configure<OpenAIConfiguration>(
            configuration.GetSection(OpenAIConfiguration.SectionName));

        // ------------------------------------------------------------
        // CORE SERVICES
        // ------------------------------------------------------------
        builder.Services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        builder.Services.AddScoped<HybridChatService>(); // ← Required for V2, not needed for V3
        builder.Services.AddScoped<InsuranceAgentServiceV2>();
        builder.Services.AddScoped<IChatInteropService, ChatInteropService>();
        builder.Services.AddScoped<IDocumentEmbeddingService, DocumentEmbeddingService>();
        builder.Services.AddScoped<INavigationEventService, NavigationEventService>();

        // ------------------------------------------------------------
        // AUTOMAPPER CONFIG
        // ------------------------------------------------------------
        builder.Services.AddAutoMapper(typeof(MappingProfile));


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
        // HTTP CLIENT FOR API CALLS
        // ------------------------------------------------------------
        builder.Services.AddHttpClient<LeadsService>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5031/"); // your API port
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
