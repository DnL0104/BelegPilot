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
        // Singleton: TesseractEngine is expensive to construct (loads ~10 MB language data
        // and initialises the LSTM model). Reused across requests, with internal locking
        // because Tesseract is not thread-safe.
        services.Configure<TesseractOptions>(configuration.GetSection(TesseractOptions.SectionName));
        services.AddSingleton<IImageTextExtractor, TesseractImageTextExtractor>();

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

        // Token / credit system
        services.AddScoped<ITokenService, TokenService>();

        // Classification — AI decides first, user confirms manually via UI.
        // No keyword rules are used.
        services.AddScoped<IClassificationService, AiOnlyClassificationService>();

        // Parsers — order matters: specific parsers first, generic last
        services.AddScoped<IReceiptParser, AmazonParser>();
        services.AddScoped<IReceiptParser, EdukiParser>();
        services.AddScoped<IReceiptParser, GenericParser>();

        return services;
    }
}
