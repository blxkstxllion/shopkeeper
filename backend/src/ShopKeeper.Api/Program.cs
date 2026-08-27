using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ShopKeeper.Api.Middleware;
using ShopKeeper.Application;
using ShopKeeper.Infrastructure;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

// Required one-time global setting for QuestPDF (report export PDF rendering) - Community
// license is free for organizations under $1M USD annual gross revenue, true here.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";
var frontendOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "The Shop Keeper API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token.",
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, [] }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// /health/live: process is up, no dependency checks - what the Docker HEALTHCHECK and a
// load balancer's liveness probe should hit. /health/ready: additionally confirms the
// database is actually reachable - what a readiness probe (safe to route traffic) should hit.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            // Response headers are hidden from JS by default unless explicitly exposed - needed
            // so the report export's real filename (Content-Disposition) reaches the frontend
            // instead of silently falling back to a client-generated one.
            .WithExposedHeaders("Content-Disposition");
    });
});

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Without this, the handler remaps well-known claims (e.g. "sub" -> ClaimTypes.NameIdentifier)
    // per JwtSecurityTokenHandler.DefaultInboundClaimTypeMap, breaking CurrentUserService's lookups
    // by the original JWT claim names ("sub", etc.) generated in JwtTokenService.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
});

builder.Services.AddAuthorization();

// Throttles the unauthenticated endpoints that are the actual brute-force/abuse targets
// (credential stuffing on login, mass fake-account creation, join-code guessing) - not
// applied API-wide, since every other endpoint already requires a valid JWT. Partitioned
// by client IP; the 6th request within a minute from the same IP is rejected outright
// (QueueLimit 0 - an attacker's request should fail fast, not eventually succeed once
// queued capacity frees up).
const string AuthRateLimitPolicy = "auth";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var payload = new
        {
            title = "Too many attempts. Please wait a moment and try again.",
            status = StatusCodes.Status429TooManyRequests,
            errors = (object?)null,
        };
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), ct);
    };

    options.AddPolicy(AuthRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

// Trusts X-Forwarded-For unconditionally (KnownNetworks/KnownProxies cleared) so
// RemoteIpAddress - and therefore the rate limiter's per-IP partitioning below - resolves
// the real client IP, not the Nginx container's. Safe specifically because of this app's
// deployment topology: per docker-compose.prod.yml, the api container is never published
// to the host - Nginx (frontend container) is the only public ingress and is what sets
// this header (frontend/nginx.conf) - so there's no path for an external client to spoof
// it by talking to the API directly.
var forwardedHeadersOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
// ForwardedHeadersOptions ships pre-populated with loopback-only trusted entries;
// clearing them is what actually makes the middleware trust the header from any hop,
// not the property assignment above - see the comment on this middleware for why that's
// safe in this specific deployment.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Uploaded files (product images, ...) are served back out as plain static files - viewing
// one needs no Authorization header, matching how a public object-storage URL would behave.
// The upload endpoint itself (UploadsController) is what's actually authorized.
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
});

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can bootstrap the same pipeline.</summary>
public partial class Program;
