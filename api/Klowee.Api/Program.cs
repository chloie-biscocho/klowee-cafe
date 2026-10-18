using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Klowee.Api.Common;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
// Locally, secrets come from user-secrets; on the host, from environment
// variables named Section__Key (ConnectionStrings__Default, Jwt__Key, ...).
// Everything required is checked here, together, so a misconfigured deploy
// fails once with the full list (docs/decisions/013-hosting-and-config.md).
var missing = RequiredConfiguration.Missing(builder.Configuration, builder.Environment);
if (missing.Count > 0)
{
    throw new InvalidOperationException(
        $"Missing configuration for the {builder.Environment.EnvironmentName} environment: " +
        $"{string.Join(", ", missing)}. Set them as environment variables on the host, " +
        "or with dotnet user-secrets locally (use ':' instead of '__', e.g. \"Jwt:Key\").");
}

// ---- Logging ----
// On the host, one JSON object per line: Render's log viewer keeps each entry
// whole and its fields searchable. Locally, the readable default.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

// One line per request: method, path, status and duration. No headers, so an
// Authorization token can never reach the logs. /api/health is skipped below.
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
                            | HttpLoggingFields.RequestPath
                            | HttpLoggingFields.ResponseStatusCode
                            | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

// ---- Services ----
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as strings ("PopUp"), not ordinals, so the contract stays
        // readable and stable if the enum is ever reordered.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // An empty or whitespace-only string on the way in means "not set"
        // (docs/decisions/008-empty-strings-are-null.md).
        options.JsonSerializerOptions.Converters.Add(new EmptyStringToNullConverter());
    });

// EF Core + PostgreSQL (Supabase). The connection string comes from configuration:
//   * local dev  -> dotnet user-secrets ("ConnectionStrings:Default")
//   * production -> host environment variable (ConnectionStrings__Default)
var connectionString = builder.Configuration.GetConnectionString("Default")!;

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

// "Today" is the date in the business's time zone, not UTC
// (docs/decisions/006-business-timezone.md). Both are singletons: the zone is
// resolved once, and TimeProvider.System is stateless.
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();

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
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<ISiteSettingService, SiteSettingService>();
builder.Services.AddScoped<IPublicHomeService, PublicHomeService>();

// ---- Media storage ----
// Supabase Storage over its REST API (docs/decisions/009-media-storage.md).
// The service role key is a secret: it lives in user-secrets locally and an
// environment variable in production, and never leaves this process.
builder.Services.Configure<SupabaseOptions>(builder.Configuration.GetSection(SupabaseOptions.SectionName));

var supabaseOptions = builder.Configuration
    .GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();

builder.Services.AddHttpClient(SupabaseStorageService.HttpClientName, client =>
{
    if (!string.IsNullOrWhiteSpace(supabaseOptions.ProjectUrl))
    {
        client.BaseAddress = new Uri(supabaseOptions.ProjectUrl + "/");
    }

    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IUploadService, UploadService>();

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

// ---- CORS ----
// The browsers that may call the API: the two Vite dev servers locally, and
// App:AllowedOrigins (the Vercel URLs) on the host, where it is required.
var allowedOrigins = builder.Environment.IsDevelopment()
    ? ["http://localhost:5173", "http://localhost:5174"]
    : (builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions())
        .ParseAllowedOrigins();

const string frontendsCorsPolicy = "Frontends";
builder.Services.AddCors(options =>
    options.AddPolicy(frontendsCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ---- Behind Render's proxy ----
// Render terminates TLS and forwards plain HTTP with X-Forwarded-For/-Proto.
// Its proxy addresses are not published, so every forwarder is trusted; the
// container only accepts traffic from Render's network anyway.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---- Rate limiting ----
// POST /api/auth/login only: 5 attempts per minute per client IP, then 429.
// The IP is the real one because UseForwardedHeaders runs first.
builder.Services.AddOptions<LoginRateLimitOptions>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(LoginRateLimitOptions.PolicyName, httpContext =>
    {
        var limits = httpContext.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
        var ip = httpContext.Connection.RemoteIpAddress ?? IPAddress.None;

        return RateLimitPartition.GetFixedWindowLimiter(ip.ToString(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.PermitLimit,
            Window = limits.Window,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// ---- Startup database work ----
// Development migrates and seeds everything; Production only makes sure the
// two owners exist. Production schema changes are the deploy workflow's job.
var plan = StartupPlan.For(app.Environment);
if (plan.Migrate || plan.SeedOwners || plan.SeedSampleData)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<KloweeDbContext>();
    var configuration = services.GetRequiredService<IConfiguration>();
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

    if (plan.Migrate)
    {
        await db.Database.MigrateAsync();
    }

    try
    {
        if (plan.SeedOwners)
        {
            await DbSeeder.SeedOwnersAsync(db, configuration, services.GetRequiredService<IPasswordHasher<User>>(), logger);
        }

        if (plan.SeedSampleData)
        {
            await DbSeeder.SeedSampleDataAsync(db);
        }
    }
    catch (Exception ex) when (!app.Environment.IsDevelopment())
    {
        // A paused or unreachable database must not stop the API from starting;
        // /api/health reports it, and the next start seeds.
        logger.LogError(ex, "Owner seeding failed at startup.");
    }
}

// ---- HTTP pipeline ----
app.UseForwardedHeaders();

// Outermost after the proxy headers, so the logged status is the one the
// client got: a domain error becomes its 4xx in the handler below, inside this.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/health"),
    branch => branch.UseHttpLogging());

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Locally only: on Render, TLS ends at its edge, which already redirects
    // HTTP to HTTPS, and the container itself serves plain HTTP.
    app.UseHttpsRedirection();
}

app.UseCors(frontendsCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so the integration test project can boot the real app.</summary>
public partial class Program;
