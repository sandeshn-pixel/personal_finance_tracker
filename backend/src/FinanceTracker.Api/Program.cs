using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FinanceTracker.Api.Configuration;
using FinanceTracker.Api.HealthChecks;
using FinanceTracker.Api.HostedServices;
using FinanceTracker.Api.Middleware;
using FinanceTracker.Api.Options;
using FinanceTracker.Api.Services;
using FinanceTracker.Application;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Infrastructure.Auth;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    EnvFileLoader.LoadIfPresent(builder.Environment.ContentRootPath);
}

builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string is not configured.");

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32, "JWT signing key must be at least 32 characters.")
    .Validate(options => options.AccessTokenLifetimeMinutes > 0, "JWT access token lifetime must be greater than zero.")
    .Validate(options => options.RefreshTokenLifetimeDays > 0, "JWT refresh token lifetime must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
    .Validate(options => builder.Environment.IsDevelopment() || options.AllowedOrigins.Length > 0, "Frontend allowed origins must be configured outside development.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AutomationOptions>()
    .Bind(builder.Configuration.GetSection(AutomationOptions.SectionName))
    .Validate(options => options.PollingIntervalSeconds >= 15, "Automation polling interval must be at least 15 seconds.")
    .Validate(options => options.GoalReminderLookaheadDays is >= 1 and <= 30, "Goal reminder lookahead must be between 1 and 30 days.")
    .Validate(options => options.MaxRecurringRetryAttempts is >= 1 and <= 10, "Automation retry attempts must be between 1 and 10.")
    .Validate(options => options.InitialRetryDelaySeconds >= 15, "Automation initial retry delay must be at least 15 seconds.")
    .Validate(options => options.MaxRetryDelaySeconds >= options.InitialRetryDelaySeconds, "Automation max retry delay must be greater than or equal to the initial retry delay.")
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FromAddress), "Email from address is required when email delivery is enabled.")
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.SmtpHost), "SMTP host is required when email delivery is enabled.")
    .Validate(options => !options.Enabled || options.Port > 0, "SMTP port must be greater than zero when email delivery is enabled.")
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var frontendOptions = builder.Configuration.GetSection(FrontendOptions.SectionName).Get<FrontendOptions>() ?? new FrontendOptions();
var allowedOrigins = frontendOptions.AllowedOrigins.Length > 0
    ? frontendOptions.AllowedOrigins
    : builder.Environment.IsDevelopment()
        ? ["http://localhost:5173", "https://localhost:5173"]
        : [];

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Too many requests.",
            Detail = "Please wait a moment before retrying this operation.",
            Status = StatusCodes.Status429TooManyRequests
        }, cancellationToken: cancellationToken);
    };

    options.AddPolicy("AuthSensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth-sensitive:{GetRequestIdentity(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("AuthSession", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"auth-session:{GetRequestIdentity(context)}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("ReportHeavy", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"report-heavy:{GetUserOrIpIdentity(context)}",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 12,
                TokensPerPeriod = 12,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("InsightsRead", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"insights-read:{GetUserOrIpIdentity(context)}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("WorkspaceUi", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"workspace-ui:{GetUserOrIpIdentity(context)}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 90,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("ExportHeavy", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"export-heavy:{GetUserOrIpIdentity(context)}",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 4,
                TokensPerPeriod = 4,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHostedService<FinanceAutomationHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapControllers();

app.Run();

static string GetRequestIdentity(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',')[0].Trim();
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string GetUserOrIpIdentity(HttpContext context)
{
    var userId = context.User.FindFirst("sub")?.Value;
    return !string.IsNullOrWhiteSpace(userId) ? userId : GetRequestIdentity(context);
}


