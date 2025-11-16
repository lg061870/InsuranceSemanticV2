#pragma warning disable SKEXP0010

using ConversaCore;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.Topics;
using InsuranceAgent.Configuration;
using InsuranceAgent.Extensions;
using InsuranceAgent.Repositories;
using InsuranceAgent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using System.Diagnostics;

internal class Program {
    private static void Main(string[] args) {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🟢 Starting InsuranceAgent (PID={Environment.ProcessId}, Thread={Environment.CurrentManagedThreadId})");

        // ------------------------------------------------------------
        // CONFIGURE BUILDER & LOGGING
        // ------------------------------------------------------------
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] ⚙️ Building services...");

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddControllers();

        // ------------------------------------------------------------
        // CONVERSACORE & INSURANCE TOPICS
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🧠 Registering ConversaCore...");
        builder.Services.AddConversaCore(builder.Configuration);

        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 💬 Registering InsuranceTopics...");
        builder.Services.AddInsuranceTopics();

        builder.Services.Configure<OpenAIConfiguration>(
            builder.Configuration.GetSection(OpenAIConfiguration.SectionName));

        // ------------------------------------------------------------
        // CORE SERVICES & LLM INTEGRATIONS
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🧩 Registering Agent Services...");
        builder.Services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        builder.Services.AddScoped<HybridChatService>();
        builder.Services.AddScoped<InsuranceAgentService>();
        builder.Services.AddScoped<IChatInteropService, ChatInteropService>();
        //builder.Services.AddScoped<ChatService>();
        builder.Services.AddScoped<IDocumentEmbeddingService, DocumentEmbeddingService>();
        builder.Services.AddScoped<INavigationEventService, NavigationEventService>();

        builder.Services.AddOpenAIEmbeddingGenerator(
            modelId: "text-embedding-3-small",
            apiKey: builder.Configuration["OpenAI:ApiKey"]
                    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "");

        builder.Services.AddSingleton<IVectorDatabaseService, SqliteVectorDatabaseService>();
        builder.Services.AddHttpClient();        // ------------------------------------------------------------
        // INSURANCE RULES REPOSITORY
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 📚 Registering InsuranceRuleRepository...");
        builder.Services.AddSingleton<InsuranceRuleRepository>();



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
        // TOPIC CONFIGURATION
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🔍 Configuring topics...");
        try {
            using var scope = app.Services.CreateScope();
            Console.WriteLine($"[{sw.ElapsedMilliseconds}ms]   ↳ Created DI scope");
            var topicRegistry = scope.ServiceProvider.GetRequiredService<TopicRegistry>();
            Console.WriteLine($"[{sw.ElapsedMilliseconds}ms]   ↳ Got TopicRegistry");
            topicRegistry.ConfigureTopics(scope.ServiceProvider);
            Console.WriteLine($"[{sw.ElapsedMilliseconds}ms]   ✅ Topic configuration finished");

            // 🔹 Test insurance rule repository load
            var repo = scope.ServiceProvider.GetRequiredService<InsuranceRuleRepository>();

        } catch (Exception ex) {
            Console.WriteLine($"[{sw.ElapsedMilliseconds}ms]   ❌ ERROR configuring topics or rules: {ex}");
        }

        // ------------------------------------------------------------
        // SQLITE HEALTH CHECK
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🗃️ SQLite health check...");
        try {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "vectorstore.db");
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            Console.WriteLine($"✅ SQLite ready at: {dbPath}");
        } catch (Exception ex) {
            Console.WriteLine($"❌ SQLite init failed: {ex.Message}");
        }

        // ------------------------------------------------------------
        // STARTUP COMPLETE
        // ------------------------------------------------------------
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] 🚀 Application startup complete. Listening...");
        app.Run();
    }
}
