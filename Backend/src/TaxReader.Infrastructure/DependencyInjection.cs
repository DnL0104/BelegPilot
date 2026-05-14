using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));

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
