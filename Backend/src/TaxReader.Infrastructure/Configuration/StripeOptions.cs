using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace TaxReader.Infrastructure.Configuration;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool DemoMode { get; set; } = false;
    public string AppBaseUrl { get; set; } = "http://localhost:3000";
    public string BusinessAddress { get; set; } = string.Empty;
    public string KleinunternehmerNote { get; set; } =
        "Gemäß §19 UStG wird keine Umsatzsteuer berechnet.";
    public PricePack[] PricePacks { get; set; } = [];
}

public record PricePack(int Credits, string StripePriceId);

/// <summary>
/// D-13: Prevents production deployment with test API keys.
/// Validates SecretKey + WebhookSecret are present and correct for the environment.
/// </summary>
public sealed class StripeOptionsValidator(IWebHostEnvironment env)
    : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            return ValidateOptionsResult.Fail("Stripe:SecretKey ist nicht konfiguriert.");

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            return ValidateOptionsResult.Fail("Stripe:WebhookSecret ist nicht konfiguriert.");

        // D-13: Production + test key = hard fail — prevents accidental test-mode launch
        if (env.IsProduction() && options.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Stripe SecretKey ist ein Testschlüssel in einer Production-Umgebung.");

        return ValidateOptionsResult.Success;
    }
}
