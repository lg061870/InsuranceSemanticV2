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
        policy.WithOrigins("http://localhost:5033", "https://localhost:7089")
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

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

// Enable CORS
app.UseCors("AllowBlazorWasm");

// Enable authentication & authorization (must be before MapApiEndpoints)
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "InsuranceSemanticV2 API running.");

// Map SignalR hub
app.MapHub<LeadsHub>("/hubs/leads");

app.MapApiEndpoints();   // ⭐ THIS IS EVERYTHING

// Comment out HTTPS redirection for development (HTTP only)
// app.UseHttpsRedirection();
app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
