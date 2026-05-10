using FluentAssertions;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.UnitTests.Configuration;

public class AnthropicOptionsTests
{
    [Fact]
    public void Default_Model_IsHaiku4_5()
    {
        var options = new AnthropicOptions();
        options.Model.Should().Be("claude-haiku-4-5");
    }

    [Fact]
    public void Default_CostPerClassification_IsOne()
    {
        var options = new AnthropicOptions();
        options.CostPerClassification.Should().Be(1);
    }

    [Fact]
    public void Default_ApiKey_IsNull()
    {
        var options = new AnthropicOptions();
        options.ApiKey.Should().BeNull();
    }

    [Fact]
    public void SectionName_IsAnthropic()
    {
        AnthropicOptions.SectionName.Should().Be("Anthropic");
    }
}
