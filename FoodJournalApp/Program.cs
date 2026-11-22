using System.Text;
using FoodJournalApp.Data;
using FoodJournalApp.Domain;
using FoodJournalApp.Rules;
using FoodJournalApp.Rules.Rules;
using FoodJournalApp.Security;
using FoodJournalApp.Storage;
using FoodJournalApp.Vision;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms like Railway assign the port via a PORT env var. Bind to all
// interfaces on it so the platform's proxy can reach the container. Locally, PORT is
// unset and launchSettings.json / ASPNETCORE_URLS apply as usual.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();

// PostgreSQL via EF Core (Npgsql). Connection string comes from DATABASE_URL or
// ConnectionStrings:Default (user-secrets locally, env vars when deployed), never
// from appsettings.json. Accepts key-value or postgres:// URI form.
var connectionString = PostgresConnectionString.Resolve(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// Identity, EF Core-backed, no cookie/UI pieces — this is a JWT-only API.
builder.Services.AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = SecretConfig.Require(builder.Configuration, "Jwt:Key");
var jwtIssuer = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var jwtExpiryMinutes = int.Parse(jwtSection["ExpiryMinutes"] ?? "60");

builder.Services.AddSingleton(new JwtTokenService(jwtKey, jwtIssuer, jwtAudience, jwtExpiryMinutes));

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// The vision seam. HttpClient is pooled by the factory. Swap this one line to change provider.
builder.Services.AddHttpClient<IVisionAnalyzer, ClaudeVisionAnalyzer>();

// The photo storage seam. PhotoStorage:Provider = "R2" (default) or "Local".
// R2 settings are validated here so a bad deploy fails at startup, not on first upload.
builder.Services.AddPhotoStorage(builder.Configuration);

// Rules engine + every rule. Register new rules here; the engine picks them all up.
builder.Services.AddScoped<INudgeRule, FriedFoodFrequencyRule>();
builder.Services.AddScoped<INudgeRule, LateNightEatingRule>();
builder.Services.AddScoped<RulesEngine>();
// Shared nudge evaluation (lookback window + engine call) used by meals and nudges endpoints.
builder.Services.AddScoped<NudgeEvaluator>();

// Let the React dev server talk to the API during development.
builder.Services.AddCors(o => o.AddPolicy("dev", p => p
    .WithOrigins("http://localhost:5173")   // Vite default
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await ApplyMigrationsAsync(app);

app.UseCors("dev");
// Serves wwwroot/uploads/* for LocalPhotoStorage and for legacy "/uploads/..." photo URLs.
// R2 photos are served by Cloudflare directly and never touch this.
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Applies pending EF Core migrations on startup. Migrate() only applies migrations that
// haven't run yet; it never drops or recreates anything. A freshly started database (e.g.
// Railway cold start) can refuse connections briefly, so transient errors are retried.
// If it still fails, the exception is rethrown so the app stops instead of serving
// requests against a missing schema, and the platform restarts it.
static async Task ApplyMigrationsAsync(WebApplication app)
{
    const int maxAttempts = 5;
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrations");

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Database schema is up to date. No migrations to apply.");
                return;
            }

            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts &&
                                   ex is Npgsql.NpgsqlException { IsTransient: true } or TimeoutException)
        {
            var delay = TimeSpan.FromSeconds(attempt * 2);
            logger.LogWarning(ex,
                "Database not reachable yet (attempt {Attempt}/{Max}). Retrying in {Delay}s.",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Applying database migrations failed. The app will stop.");
            throw;
        }
    }
}
