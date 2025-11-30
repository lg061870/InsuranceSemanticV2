using InsuranceSemanticV2.Api.Endpoints;
using InsuranceSemanticV2.Data.DataContext;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.MapGet("/", () => "InsuranceSemanticV2 API running.");

app.MapApiEndpoints();   // ⭐ THIS IS EVERYTHING

app.UseHttpsRedirection();
app.Run();
