using FluentAssertions;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Domain;

public class ReceiptFileTests
{
    [Fact]
    public void CreateReceiptFile_DefaultStatus_IsUploaded()
    {
        var file = new ReceiptFile();

        file.Status.Should().Be(FileStatus.Uploaded);
    }

    [Fact]
    public void CreateReceiptFile_DefaultUploadedAt_IsRecentUtc()
    {
        var before = DateTime.UtcNow;
        var file = new ReceiptFile();
        var after = DateTime.UtcNow;

        file.UploadedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CreateReceiptFile_WithFactory_HasCorrectDefaults()
    {
        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Amazon");

        file.SourceHint.Should().Be("Amazon");
        file.Status.Should().Be(FileStatus.Uploaded);
        file.Id.Should().NotBeEmpty();
    }
}
