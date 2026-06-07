using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.IntegrationTests.Fixtures;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.IntegrationTests;

/// <summary>
/// QA-01: proves FK ON DELETE CASCADE for ReceiptFile → Receipt / ReceiptItem / ProcessingRun
/// against real Postgres. The in-memory provider approximates cascade; only Postgres enforces the DDL.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class CascadeDeleteTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task DeleteReceiptFile_CascadesToChildren_RemovesReceiptAndItemsAndRun()
    {
        // Arrange — seed User → ReceiptFile → Receipt → ReceiptItem → ProcessingRun.
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        await using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Id = userId,
                Email = $"cascade-{userId:N}@test.local",
                DisplayName = "Cascade Tester",
                PasswordHash = "x"
            });

            var file = TestDataFactory.CreateReceiptFile(id: fileId, contentHash: "cascade-hash-01");
            file.UserId = userId;
            ctx.ReceiptFiles.Add(file);

            ctx.Receipts.Add(TestDataFactory.CreateReceipt(id: receiptId, receiptFileId: fileId));

            ctx.ReceiptItems.Add(TestDataFactory.CreateReceiptItem(id: itemId, receiptId: receiptId));

            ctx.ProcessingRuns.Add(new ProcessingRun
            {
                Id = runId,
                ReceiptFileId = fileId,
                Status = ProcessingStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });

            await ctx.SaveChangesAsync();
        }

        // Act — remove the ReceiptFile; cascade should delete all children.
        await using (var ctx = CreateContext())
        {
            var file = await ctx.ReceiptFiles.FindAsync(fileId);
            file.Should().NotBeNull();
            ctx.ReceiptFiles.Remove(file!);
            await ctx.SaveChangesAsync();
        }

        // Assert — verify via a fresh context that children are gone.
        await using var verifyCtx = CreateContext();

        var receiptExists = await verifyCtx.Receipts.AnyAsync(r => r.Id == receiptId);
        receiptExists.Should().BeFalse("FK ON DELETE CASCADE must remove the Receipt");

        var itemExists = await verifyCtx.ReceiptItems.AnyAsync(i => i.Id == itemId);
        itemExists.Should().BeFalse("FK ON DELETE CASCADE must remove the ReceiptItem");

        var runExists = await verifyCtx.ProcessingRuns.AnyAsync(r => r.Id == runId);
        runExists.Should().BeFalse("FK ON DELETE CASCADE must remove the ProcessingRun");
    }
}
