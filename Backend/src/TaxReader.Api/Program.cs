using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
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

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BelegPilot API");

    var builder = WebApplication.CreateBuilder(args);

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
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCors();
    app.UseSerilogRequestLogging();
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
