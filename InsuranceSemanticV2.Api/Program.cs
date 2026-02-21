using System.Text;
using InsuranceSemanticV2.Api.Endpoints;
using InsuranceSemanticV2.Api.Hubs;
using InsuranceSemanticV2.Api.Services;
using InsuranceSemanticV2.Data.DataContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Register SignalR
builder.Services.AddSignalR();

// Register JWT Token Service
builder.Services.AddScoped<JwtTokenService>();

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };

        // Allow SignalR to pass token via query string (access_token parameter)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // If the request is for the SignalR hub
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Register Lead Lifecycle Service
builder.Services.Configure<LeadLifecycleOptions>(
    builder.Configuration.GetSection("LeadLifecycle"));
builder.Services.AddScoped<LeadLifecycleService>();

// Register Session Cleanup Background Service
builder.Services.AddHostedService<SessionCleanupService>();

// Add CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5033",
            "https://localhost:7089",
            "http://localhost:5122",
            "https://localhost:7058",
            "http://origovs-001-site1.ntempurl.com",
            "https://origovs-001-site1.ntempurl.com"
        )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// Support both SQL Server (production) and InMemory (testing)
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
if (useInMemoryDatabase)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

var app = builder.Build();

// Create logs directory and write startup diagnostics (non-blocking)
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(1000); // Let app start first
        
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        var startupLog = Path.Combine(logsDir, $"startup-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        
        var logLines = new List<string>
        {
            "=== API Starting ===",
            $"Time: {DateTime.UtcNow}",
            $"Environment: {app.Environment.EnvironmentName}",
            $"ContentRootPath: {app.Environment.ContentRootPath}",
            $"BaseDirectory: {AppContext.BaseDirectory}",
            $"PathBase: {app.Configuration["PathBase"]}",
            $"UseInMemoryDatabase: {app.Configuration.GetValue<bool>("UseInMemoryDatabase")}",
            $"ConnectionString: {app.Configuration.GetConnectionString("DefaultConnection")?.Split("Password=")[0]}...",
        };
        
        // Test database connection (with timeout)
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var canConnect = await dbContext.Database.CanConnectAsync(cts.Token);
            logLines.Add($"Database connection test: {(canConnect ? "SUCCESS" : "FAILED")}");
        }
        catch (Exception dbEx)
        {
            logLines.Add($"Database connection error: {dbEx.GetType().Name} - {dbEx.Message}");
        }
        
        File.WriteAllLines(startupLog, logLines);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to write startup log: {ex.Message}");
    }
});

// Configure base path for virtual directory deployment
var pathBase = app.Configuration.GetValue<string>("PathBase");
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

var enableSwagger = app.Configuration.GetValue<bool>("EnableSwagger");
if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Use path relative to /api/swagger so final URL is /api/swagger/v1/swagger.json
        options.SwaggerEndpoint("../swagger/v1/swagger.json", "InsuranceSemanticV2 API v1");
        options.RoutePrefix = "swagger";
    });
    app.MapOpenApi();
}

// Enable CORS
app.UseCors("AllowBlazorWasm");

// Enable authentication & authorization (must be before MapApiEndpoints)
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "InsuranceSemanticV2 API running.");

// Health check endpoint with database test
app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new
        {
            Status = "Healthy",
            DatabaseConnection = canConnect ? "Connected" : "Failed",
            Timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            Status = "Unhealthy",
            DatabaseConnection = "Error",
            Error = ex.Message,
            Timestamp = DateTime.UtcNow
        });
    }
});

// Map SignalR hub
app.MapHub<LeadsHub>("/hubs/leads");

app.MapApiEndpoints();   // ⭐ THIS IS EVERYTHING

// Comment out HTTPS redirection for development (HTTP only)
// app.UseHttpsRedirection();
app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
