using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Parsers;
using TaxReader.Infrastructure.Services;
using TaxReader.Infrastructure.Storage;

namespace TaxReader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // PDF extraction
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();

        // Auth
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // RefreshToken:HashKey is a 32-byte Base64-encoded HMAC-SHA256 pepper (D-01).
        // ValidateOnStart() runs RefreshTokenOptionsValidator at host build so a missing
        // or malformed value fails the boot loudly instead of silently degrading the
        // HMAC to an empty-key construction (CR-01 from the Phase 2 review).
        services.AddSingleton<IValidateOptions<RefreshTokenOptions>, RefreshTokenOptionsValidator>();
        services
            .AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .ValidateOnStart();

        // Anthropic / AI classification
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddHttpClient<IAiClassifier, ClaudeAiClassifier>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Image OCR (local Tesseract — no API costs).
        // D-16/D-17/D-18: bounded TesseractEngine pool replaces the legacy Singleton+lock.
        // PoolSize (default 3) is configurable via Tesseract__PoolSize. The pool is registered
        // as Singleton so its Channel<TesseractEngine> survives the host lifetime. Both the
        // concrete type and the IImageTextExtractor interface resolve to the SAME instance
        // (the second registration delegates to the first) so the warmup service finds the
        // same pool the OCR callers use.
        services.Configure<TesseractOptions>(configuration.GetSection(TesseractOptions.SectionName));
        services.AddSingleton<TesseractEnginePool>();
        services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<TesseractEnginePool>());
        services.AddHostedService<TesseractEnginePoolWarmupService>();

        // Hangfire — RESEARCH § Pattern 1 + Pitfall 1.
        // D-04: tiered retries are applied per-job via [AutomaticRetry] attributes.
        // D-16/D-17: WorkerCount is aligned with Tesseract:PoolSize so jobs never
        // out-pace the OCR engine pool. Test branch swaps Postgres → in-memory storage
        // so WAF tests don't need a real DB.
        var useInMemory = string.Equals(
            configuration["Hangfire:UseInMemoryStorage"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        services.AddHangfire(cfg =>
        {
            cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
               .UseSimpleAssemblyNameTypeSerializer()
               .UseRecommendedSerializerSettings();
            if (useInMemory)
            {
                cfg.UseMemoryStorage();
            }
            else
            {
                cfg.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(
                    configuration.GetConnectionString("DefaultConnection")),
                    new PostgreSqlStorageOptions
                    {
                        PrepareSchemaIfNecessary = true,
                        QueuePollInterval = TimeSpan.FromSeconds(1),
                        InvisibilityTimeout = TimeSpan.FromMinutes(30)
                    });
            }
        });

        var poolSize = configuration.GetValue<int>("Tesseract:PoolSize", 3);
        services.AddHangfireServer(options =>
        {
            // D-16 + RESEARCH Pitfall 7: WorkerCount aligned with Tesseract pool size —
            // never more workers than engines, so engine starvation is impossible.
            options.WorkerCount = poolSize;
            options.Queues = ["default"];
            options.CancellationCheckInterval = TimeSpan.FromSeconds(2);
        });

        // D-08: read Hangfire:SeedAdminEmails on startup; flip IsAdmin=true on matches.
        services.AddHostedService<Services.AdminBootstrap.SeedAdminUsersHostedService>();

        // Application-layer port wrapping Hangfire's IBackgroundJobClient so Application
        // (and tests) stay Hangfire-storage-free. The adapter forwards Enqueue/Delete to
        // the framework client registered above by AddHangfire().
        services.AddScoped<Application.Interfaces.IBackgroundJobClient, HangfireBackgroundJobClient>();

        // Upload blob store — persists file bytes across the HTTP→Hangfire boundary.
        // FileSystemUploadBlobStore defaults to Path.GetTempPath()/taxreader-uploads when
        // UploadStorage:Path is not configured (safe default for dev).
        services.Configure<UploadStorageOptions>(configuration.GetSection(UploadStorageOptions.SectionName));
        services.AddSingleton<IUploadBlobStore, FileSystemUploadBlobStore>();

        // Token / credit system
        services.AddScoped<ITokenService, TokenService>();

        // Phase 4 D-07: HybridClassificationService is the registered IClassificationService.
        // AiOnlyClassificationService stays as a concrete registration so HybridClassificationService
        // can inject it directly. RuleBasedClassifier is injected into HybridClassificationService.
        services.AddScoped<AiOnlyClassificationService>();
        services.AddScoped<RuleBasedClassifier>();
        services.AddScoped<IClassificationService, HybridClassificationService>();

        // Parsers — order matters: specific parsers first, generic last
        services.AddScoped<IReceiptParser, AmazonParser>();
        services.AddScoped<IReceiptParser, EdukiParser>();
        services.AddScoped<IReceiptParser, GenericParser>();

        return services;
    }
}
