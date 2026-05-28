using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using TaxReader.Infrastructure.Configuration;
using Xunit;

namespace TaxReader.UnitTests.Configuration;

public class StripeOptionsValidatorTests
{
    private static Mock<IWebHostEnvironment> ProductionEnv()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");
        return env;
    }

    private static Mock<IWebHostEnvironment> DevelopmentEnv()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return env;
    }

    [Fact]
    public void Validate_ProductionWithTestKey_ThrowsInvalidOperationException()
    {
        // D-13: Production + sk_test_ must throw to prevent accidental live deployment with test keys
        var validator = new StripeOptionsValidator(ProductionEnv().Object);
        var options = new StripeOptions
        {
            SecretKey = "sk_test_EXAMPLE12345",
            WebhookSecret = "whsec_test"
        };

        var act = () => validator.Validate(null, options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Testschlüssel*Production*");
    }

    [Fact]
    public void Validate_MissingSecretKey_ReturnsFail()
    {
        var validator = new StripeOptionsValidator(DevelopmentEnv().Object);
        var options = new StripeOptions { SecretKey = "", WebhookSecret = "whsec_test" };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SecretKey");
    }

    [Fact]
    public void Validate_MissingWebhookSecret_ReturnsFail()
    {
        var validator = new StripeOptionsValidator(DevelopmentEnv().Object);
        var options = new StripeOptions { SecretKey = "sk_test_EXAMPLE", WebhookSecret = "" };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("WebhookSecret");
    }

    [Fact]
    public void Validate_ValidDevelopmentTestKey_ReturnsSuccess()
    {
        // IWebHostEnvironment.IsProduction() is an extension method checking EnvironmentName == "Production"
        var validator = new StripeOptionsValidator(DevelopmentEnv().Object);
        var options = new StripeOptions
        {
            SecretKey = "sk_test_EXAMPLE12345",
            WebhookSecret = "whsec_test"
        };

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
