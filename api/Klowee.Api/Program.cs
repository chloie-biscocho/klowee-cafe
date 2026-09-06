using Klowee.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddControllers();

// EF Core + PostgreSQL (Supabase). The connection string comes from configuration:
//   * local dev  -> dotnet user-secrets ("ConnectionStrings:Default")
//   * production -> host environment variable (ConnectionStrings__Default)
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Set it via user-secrets in " +
        "development (dotnet user-secrets set \"ConnectionStrings:Default\" \"...\") " +
        "or an environment variable in production.");
}

builder.Services.AddDbContext<KloweeDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Permissive CORS — Development only.
const string devCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
    options.AddPolicy(devCorsPolicy, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---- HTTP pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(devCorsPolicy);

    // Apply migrations and seed baseline data (idempotent) for local development.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KloweeDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
