using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Sentry;
using Serilog;
using TaxReader.Api.Endpoints;
using TaxReader.Api.Middleware;
using TaxReader.Api.Services;
using TaxReader.Application.Commands;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Queries;
using TaxReader.Infrastructure;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Observability;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BelegPilot API");

    var builder = WebApplication.CreateBuilder(args);

    // Pitfall 1: Sentry must be the FIRST registration after CreateBuilder so it
    // sees DI-time exceptions. Do not move below other AddX calls.
    // DSN binds automatically from configuration (Sentry__Dsn env var or the
    // "Sentry":{"Dsn":"..."} block in appsettings.json). Empty DSN = no-op.
    // Environment is also bound from configuration (Sentry__Environment env var
    // or the SDK's ASPNETCORE_ENVIRONMENT fallback) — do not overwrite here.
    builder.WebHost.UseSentry(options =>
    {
        options.SendDefaultPii = false;
        options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
        options.SetBeforeSend((sentryEvent, hint) => SentryScrubbing.Scrub(sentryEvent));
    });

    var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Serilog
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));

    // Infrastructure (DbContext, services, parsers)
    builder.Services.AddInfrastructure(builder.Configuration);

    // JWT Authentication
    var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
    builder.Services.Configure<JwtOptions>(jwtSection);
    var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    builder.Services.AddAuthorization();

    // CurrentUser (reads from HttpContext JWT claims)
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<UploadReceiptFilesCommand>();

    // Application handlers
    builder.Services.AddScoped<DeleteAccountHandler>();
    builder.Services.AddScoped<UploadReceiptFilesHandler>();
    builder.Services.AddScoped<DeleteReceiptFileHandler>();
    builder.Services.AddScoped<BulkDeleteReceiptFilesHandler>();
    builder.Services.AddScoped<ConfirmClassificationHandler>();
    builder.Services.AddScoped<BatchConfirmHandler>();
    builder.Services.AddScoped<ReclassifyReceiptHandler>();
    builder.Services.AddScoped<GetReceiptFilesHandler>();
    builder.Services.AddScoped<GetReceiptsHandler>();
    builder.Services.AddScoped<GetReceiptByIdHandler>();
    builder.Services.AddScoped<GetReceiptItemsHandler>();
    builder.Services.AddScoped<GetCategoryTotalsHandler>();
    builder.Services.AddScoped<GetAnnualSummaryHandler>();
    builder.Services.AddScoped<GetExportDataHandler>();
    builder.Services.AddScoped<GetPendingSuggestionsHandler>();
    builder.Services.AddScoped<GetUserSettingsHandler>();
    builder.Services.AddScoped<UpdateUserSettingsHandler>();

    // D-06 + RESEARCH Pitfall 1: real client IP behind Caddy reverse proxy.
    // .NET 10: KnownNetworks is OBSOLETE (ASPDEPR005) — use KnownIPNetworks with System.Net.IPNetwork
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                 | ForwardedHeaders.XForwardedProto
                                 | ForwardedHeaders.XForwardedHost;
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
        options.ForwardLimit = 1; // Caddy is the only hop — raising this allows IP spoofing
    });

    // Rate limiting — 4 named policies + global IP-partitioned limiter. German 429 body.
    // RESEARCH Pitfall 5: RejectionStatusCode MUST be set FIRST (default is 503).
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global: 60/min per IP (D-09). Triggers BEFORE auth so unauthenticated
        // requests count against the bucket.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                }));

        // auth-strict: 5/min, partition by sub (authenticated /account) or IP (anonymous).
        // D-09 (anonymous /login, /register) + D-12 (authenticated /account, partitioned
        // by sub — plan 02-02 attaches this policy to /account).
        options.AddPolicy("auth-strict", httpContext =>
        {
            var sub = httpContext.User.FindFirst("sub")?.Value;
            var partitionKey = !string.IsNullOrEmpty(sub)
                ? $"user:{sub}"
                : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
        });

        // auth-refresh: 30/min per IP (D-05). Higher than auth-strict because legitimate
        // token rotation may burst (multiple tabs, app cold-starts).
        options.AddPolicy("auth-refresh", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                }));

        // upload-concurrency: 2 active + 4 queued, partitioned by authenticated user (D-07).
        // Retired in Phase 3 (PIPE-02) when uploads become 202 Accepted + Hangfire jobs.
        options.AddPolicy("upload-concurrency", httpContext =>
        {
            var sub = httpContext.User.FindFirst("sub")?.Value ?? "anonymous";
            return RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: $"user:{sub}",
                factory: _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 2,
                    QueueLimit = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        });

        // OnRejected: German ProblemDetails + Retry-After header (D-08). T-02-07
        // mitigation: no policy name in the response body.
        options.OnRejected = async (context, cancellationToken) =>
        {
            var retryAfterSeconds = 60;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                retryAfterSeconds = (int)retryAfter.TotalSeconds;
                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfterSeconds.ToString(NumberFormatInfo.InvariantInfo);
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Zu viele Anfragen.",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = $"Bitte versuchen Sie es in {retryAfterSeconds} Sekunden erneut."
            };
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

            // Pass contentType explicitly — WriteAsJsonAsync would otherwise reset the
            // response content-type to application/json, clobbering the problem+json header.
            await context.HttpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        };
    });

    // CORS — D-07: production fail-mode is deny-all when CORS_ALLOWED_ORIGINS unset.
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (corsOrigins is { Length: > 0 })
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
                return;
            }

            if (builder.Environment.IsDevelopment())
            {
                policy.WithOrigins("http://localhost:3000")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
                return;
            }

            // D-07: non-Development with no origins → deny all. We register the
            // default policy with empty Origins so app.UseCors() doesn't error,
            // but no preflight or simple cross-origin request passes the check.
            // Same-origin browser requests via the Caddy proxy are unaffected
            // (browsers don't add Origin on same-origin requests).
            Log.Warning(
                "CORS_ALLOWED_ORIGINS unset in {Environment} environment — denying all cross-origin requests.",
                builder.Environment.EnvironmentName);
        });
    });

    // OpenAPI
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // D-02: log resolved Anthropic configuration so any drift between code,
    // compose, and env is visible without throwing. Logs once at startup.
    var resolvedAnthropicOptions = app.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
    app.Logger.LogInformation(
        "Anthropic configuration resolved: Model={Model}, CostPerClassification={CostPerClassification}",
        resolvedAnthropicOptions.Model,
        resolvedAnthropicOptions.CostPerClassification);

    // Middleware
    // D-06: UseForwardedHeaders MUST be FIRST so anything that reads RemoteIpAddress
    // (rate limiter, request logging) sees the real client IP, not Caddy's docker IP.
    app.UseForwardedHeaders();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCors();
    app.UseSerilogRequestLogging();
    // UseRateLimiter sits AFTER request logging (so 429s are logged) and BEFORE
    // auth (the global limiter partitions by IP only — endpoint policies that need
    // the sub claim run at the endpoint layer after routing/auth per RESEARCH Pitfall 2).
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // OpenAPI + Scalar (dev only)
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("BelegPilot API");
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });
    }

    // Auto-migrate in Development and when RUN_MIGRATIONS=true (e.g. self-hosted container).
    var shouldMigrate = app.Environment.IsDevelopment()
        || string.Equals(
            Environment.GetEnvironmentVariable("RUN_MIGRATIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    if (shouldMigrate)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    // Map endpoints — RequireAuthorization applies to all routes by default.
    // Individual auth endpoints use .AllowAnonymous() to opt out.
    var api = app.MapGroup("/api/v1").RequireAuthorization();

    api.MapAuthEndpoints();
    api.MapReceiptFileEndpoints();
    api.MapReceiptEndpoints();
    api.MapClassificationEndpoints();
    api.MapReportEndpoints();
    api.MapTokenEndpoints();
    api.MapSettingsEndpoints();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
