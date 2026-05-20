using FluentAssertions;
using TaxReader.Domain.Common;

namespace TaxReader.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessResult_WithValue()
    {
        var result = Result<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("hello");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_CreatesFailureResult_WithError()
    {
        var result = Result<string>.Failure("something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("something went wrong");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Success_WithIntValue_ReturnsCorrectValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_WithListType_ReturnsDefault()
    {
        var result = Result<List<string>>.Failure("not found");

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
