using System.Text;
using System.Text.Json.Serialization;
using Klowee.Api.Common;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Enums travel as strings ("PopUp"), not ordinals, so the contract stays
        // readable and stable if the enum is ever reordered.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

// ---- Authentication ----
// This API issues and validates its own JWTs (docs/decisions/004-jwt-auth.md).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 characters. Set it via user-secrets in development " +
        "(dotnet user-secrets set \"Jwt:Key\" \"<32+ chars>\") or an environment variable in production.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names the token actually carries ("sub"), instead of
        // rewriting them to the legacy WS-Federation URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

// A fallback policy applies to every endpoint that does not state its own
// requirements, so a new controller is closed by default. Public endpoints opt
// out explicitly with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// ---- Application services ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Standalone hasher from Microsoft.Extensions.Identity.Core: PBKDF2 with a
// per-password random salt. Not the full ASP.NET Core Identity stack.
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMenuCategoryService, MenuCategoryService>();
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IAddOnService, AddOnService>();
builder.Services.AddScoped<IMenuVersionService, MenuVersionService>();

// ---- Errors ----
// Every failure leaves as ProblemDetails: validation 400s from [ApiController],
// domain errors from ApiExceptionHandler, bare status codes from UseStatusCodePages.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Klowee Cafe API", Version = "v1" });

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken value only — Swagger adds the \"Bearer \" prefix."
    });

    // Applies the padlock to every operation, so the Swagger "Authorize" button
    // sends the token with each request.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
    });
});

// The two Vite dev servers (admin, client). Development only.
const string devCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
    options.AddPolicy(devCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// ---- HTTP pipeline ----
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(devCorsPolicy);

    // Apply migrations and seed baseline data (idempotent) for local development.
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<KloweeDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(
        db,
        services.GetRequiredService<IConfiguration>(),
        services.GetRequiredService<IPasswordHasher<User>>(),
        services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder)));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so the integration test project can boot the real app.</summary>
public partial class Program;
