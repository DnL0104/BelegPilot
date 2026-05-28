using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Stripe.Checkout;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Services;

/// <summary>
/// Tests for StripePaymentProvider (PAY-02 — KleinunternehmerNote invoice footer).
/// Verifies that the options built for a checkout session include the DE-compliant
/// Kleinunternehmer note per §19 UStG as the invoice footer.
/// </summary>
public class StripePaymentProviderTests
{
    private static StripePaymentProvider CreateProvider(string kleinunternehmerNote = "Gemäß §19 UStG wird keine Umsatzsteuer berechnet.")
    {
        var options = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_key",
            PublishableKey = "pk_test_key",
            WebhookSecret = "whsec_test",
            AppBaseUrl = "http://localhost:3000",
            KleinunternehmerNote = kleinunternehmerNote,
            PricePacks = [new PricePack(50, "price_50"), new PricePack(200, "price_200"), new PricePack(500, "price_500")]
        });
        return new StripePaymentProvider(options, Mock.Of<Microsoft.Extensions.Logging.ILogger<StripePaymentProvider>>());
    }

    [Fact]
    public void BuildSessionCreateOptions_IncludesInvoiceCreationEnabled()
    {
        // Arrange
        var provider = CreateProvider();

        // Act — use the internal method to inspect the options shape (PAY-02)
        var options = provider.BuildSessionCreateOptions(50, stripeCustomerId: null);

        // Assert
        options.InvoiceCreation.Should().NotBeNull();
        options.InvoiceCreation.Enabled.Should().BeTrue();
    }

    [Fact]
    public void BuildSessionCreateOptions_InvoiceFooterContainsKleinunternehmerNote()
    {
        // Arrange — PAY-02: DE-compliant Kleinunternehmer invoice note per §19 UStG
        const string expectedNote = "Gemäß §19 UStG wird keine Umsatzsteuer berechnet.";
        var provider = CreateProvider(expectedNote);

        // Act
        var options = provider.BuildSessionCreateOptions(50, stripeCustomerId: null);

        // Assert
        options.InvoiceCreation.InvoiceData.Should().NotBeNull();
        options.InvoiceCreation.InvoiceData.Footer.Should().Be(expectedNote);
    }

    [Fact]
    public void BuildSessionCreateOptions_WithoutExistingCustomer_SetsCustomerCreationAlways()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var options = provider.BuildSessionCreateOptions(50, stripeCustomerId: null);

        // Assert
        options.CustomerCreation.Should().Be("always");
        options.Customer.Should().BeNull();
    }

    [Fact]
    public void BuildSessionCreateOptions_WithExistingCustomer_PassesCustomerId()
    {
        // Arrange
        var provider = CreateProvider();
        const string customerId = "cus_test_123";

        // Act
        var options = provider.BuildSessionCreateOptions(50, stripeCustomerId: customerId);

        // Assert
        options.Customer.Should().Be(customerId);
        // CustomerCreation should be null when customer ID is provided
        options.CustomerCreation.Should().BeNull();
    }

    [Fact]
    public void BuildSessionCreateOptions_InvalidCredits_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var act = () => provider.BuildSessionCreateOptions(999, stripeCustomerId: null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*999 Credits*");
    }
}
