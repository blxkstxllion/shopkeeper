using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ShopKeeper.Api.Local;
using ShopKeeper.Api.Middleware;
using ShopKeeper.Application;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure;
using ShopKeeper.Infrastructure.Persistence;

// Required one-time global setting for QuestPDF (report export PDF rendering), same as
// ShopKeeper.Api/Program.cs - Community license is free under $1M USD annual gross revenue.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Every setting below is set directly in code rather than read from appsettings.json, even
// though every other ASP.NET Core project in this repo uses one - deliberately. This runs as a
// Tauri externalBin sidecar (see Phase 2 of the plan): Tauri's build only copies the one exe
// file a sidecar is declared as, not arbitrary sibling files sitting next to it in
// src-tauri/binaries/ - confirmed the hard way, appsettings.json silently wasn't present next to
// the running sidecar, so it fell through to the Postgres default. Rather than fighting Tauri's
// bundle.resources copying semantics for one small, fixed, never-user-configurable settings
// file, making the single published exe genuinely need nothing else beside it is simpler and
// removes an entire class of "forgot to ship a sibling file" bug for the Phase 4 installer too.
//
// %LOCALAPPDATA% is the correct per-user, always-writable location for a desktop app's own data
// on Windows, regardless of where the exe itself is installed (often Program Files, not
// writable by a standard user) - a path relative to the install directory would silently fail
// to create the DB file on a real installed copy, not just in this dev setup.
var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyShopkeeper");
Directory.CreateDirectory(appDataRoot);
builder.Configuration["ConnectionStrings:Default"] = $"Data Source={Path.Combine(appDataRoot, "shopkeeper.db")}";
builder.Configuration["Database:Provider"] = "Sqlite";
builder.Configuration["Urls"] = "http://127.0.0.1:58732";
// Never validated in this edition (no AddJwtBearer scheme) - exists only because
// CompleteOnboardingCommand internally calls TokenIssuer.IssueAsync as a side effect, which
// would throw without a syntactically-valid (32+ byte) secret. See CompleteLocalSetupCommand's
// doc comment.
builder.Configuration["Jwt:Secret"] = "offline-edition-local-only-placeholder-secret-never-validated";
builder.Configuration["Jwt:Issuer"] = "ShopKeeperLocal";
builder.Configuration["Jwt:Audience"] = "ShopKeeperLocal";

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<ILocalIdentityCache, LocalIdentityCache>();

// The only auth scheme in this edition - see LocalAuthenticationHandler's doc comment for why
// there's no JWT/login at all. No CORS restriction either: this process only ever binds to
// 127.0.0.1 as a Tauri sidecar (see Phase 2 of the plan) - nothing outside the local machine can
// ever reach it, so a permissive policy here carries none of the risk it would on the hosted API.
builder.Services.AddAuthentication(LocalAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

const string LocalCorsPolicy = "LocalCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalCorsPolicy, policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// /health/live is what the frontend's splash screen polls while waiting for the sidecar to come
// up (see Phase 2) - no dependency checks, just "is the process accepting requests yet."
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// Same %LOCALAPPDATA% directory as the DB above, not ContentRootPath - product images need to
// survive and remain writable regardless of where the sidecar binary itself is installed.
var uploadsPath = Path.Combine(appDataRoot, "uploads");
Directory.CreateDirectory(uploadsPath);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Populate the identity cache from whatever's already in the DB before accepting any request -
// required for a restart of an already-set-up install (if this only ran after setup, every
// request on a second launch would be misauthenticated as "no identity" until setup ran again,
// which it never would since CompleteLocalSetupCommand refuses to run twice).
await app.Services.GetRequiredService<ILocalIdentityCache>().RefreshAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Same reasoning as ShopKeeper.Api/Program.cs: viewing an uploaded product image needs no
// Authorization header, matching how a public object-storage URL would behave.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
});

app.UseCors(LocalCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
