using InsuranceSemanticV2.Data.DataContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace InsuranceSemanticV2.IntegrationTests;

/// <summary>
/// Base class for integration tests with in-memory database
/// </summary>
public class IntegrationTestBase : IDisposable
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly IServiceScope Scope;
    protected readonly AppDbContext DbContext;

    public IntegrationTestBase()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:SecretKey", "integration-tests-only-secret-key-32");
                builder.UseSetting("Jwt:Issuer", "InsuranceSemanticV2.IntegrationTests");
                builder.UseSetting("Jwt:Audience", "InsuranceSemanticV2.IntegrationTests");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Tell the API to use InMemory database for testing
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UseInMemoryDatabase"] = "true",
                        ["Jwt:SecretKey"] = "integration-tests-only-secret-key-32",
                        ["Jwt:Issuer"] = "InsuranceSemanticV2.IntegrationTests",
                        ["Jwt:Audience"] = "InsuranceSemanticV2.IntegrationTests"
                    });
                });
            });

        Client = Factory.CreateClient();

        // Get DbContext for test assertions
        Scope = Factory.Services.CreateScope();
        DbContext = Scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        Scope?.Dispose();
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
