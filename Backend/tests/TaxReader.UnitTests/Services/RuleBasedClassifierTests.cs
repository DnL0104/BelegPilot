using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Services;

public class RuleBasedClassifierTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    private readonly AppDbContext _dbContext;
    private readonly RuleBasedClassifier _classifier;

    public RuleBasedClassifierTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        var logger = new Mock<ILogger<RuleBasedClassifier>>().Object;
        _classifier = new RuleBasedClassifier(_dbContext, logger);
    }

    [Fact]
    public async Task ClassifyItemAsync_UserRuleMatchesBeforeSystemRule_ReturnsUserRuleClassification()
    {
        // System rule: DescriptionPattern "Buch" → WerbungskostenFachliteratur
        var systemRule = TestDataFactory.CreateRule(
            descriptionPattern: "Buch",
            userId: null,
            category: Category.WerbungskostenFachliteratur,
            priority: 10);

        // User rule: same pattern → WerbungskostenArbeitsmittel (different category)
        var userRule = TestDataFactory.CreateRule(
            descriptionPattern: "Buch",
            userId: TestUserId,
            category: Category.WerbungskostenArbeitsmittel,
            priority: 10);

        _dbContext.ClassificationRules.AddRange(systemRule, userRule);
        await _dbContext.SaveChangesAsync();

        var item = TestDataFactory.CreateReceiptItem(description: "Fachbuch Mathematik");

        var result = await _classifier.ClassifyItemAsync(item, vendor: "", sourceFileName: "", userId: TestUserId);

        result.Should().NotBeNull();
        result!.Category.Should().Be(Category.WerbungskostenArbeitsmittel);
    }

    [Fact]
    public async Task ClassifyItemAsync_NoUserRuleMatch_SystemRuleFallback_ReturnsSystemRuleClassification()
    {
        // System rule only
        var systemRule = TestDataFactory.CreateRule(
            descriptionPattern: "Buch",
            userId: null,
            category: Category.WerbungskostenFachliteratur,
            priority: 10);

        _dbContext.ClassificationRules.Add(systemRule);
        await _dbContext.SaveChangesAsync();

        var item = TestDataFactory.CreateReceiptItem(description: "Fachbuch");

        var result = await _classifier.ClassifyItemAsync(item, vendor: "", sourceFileName: "", userId: TestUserId);

        result.Should().NotBeNull();
        result!.Category.Should().Be(Category.WerbungskostenFachliteratur);
    }

    [Fact]
    public async Task ClassifyItemAsync_NoRuleMatches_ReturnsNull()
    {
        var systemRule = TestDataFactory.CreateRule(
            descriptionPattern: "Buch",
            userId: null,
            category: Category.WerbungskostenFachliteratur,
            priority: 10);

        _dbContext.ClassificationRules.Add(systemRule);
        await _dbContext.SaveChangesAsync();

        var item = TestDataFactory.CreateReceiptItem(description: "Laptop");

        var result = await _classifier.ClassifyItemAsync(item, vendor: "", sourceFileName: "", userId: TestUserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyItemAsync_VendorPatternOnly_MatchesCaseInsensitive()
    {
        // Rule fires on vendor substring match only (no DescriptionPattern)
        var rule = TestDataFactory.CreateRule(
            descriptionPattern: null,
            vendorPattern: "amazon",
            userId: null,
            category: Category.WerbungskostenArbeitsmittel,
            priority: 10);

        _dbContext.ClassificationRules.Add(rule);
        await _dbContext.SaveChangesAsync();

        var item = TestDataFactory.CreateReceiptItem(description: "Wireless Mouse");

        // Vendor "Amazon.de" contains "amazon" case-insensitively
        var result = await _classifier.ClassifyItemAsync(item, vendor: "Amazon.de", sourceFileName: "", userId: TestUserId);

        result.Should().NotBeNull();
        result!.Category.Should().Be(Category.WerbungskostenArbeitsmittel);
    }

    [Fact]
    public async Task ClassifyItemAsync_AllFieldsMustMatch_PartialMatchDoesNotFire()
    {
        // Rule requires BOTH VendorPattern AND DescriptionPattern to match
        var rule = TestDataFactory.CreateRule(
            descriptionPattern: "Buch",
            vendorPattern: "Amazon",
            userId: null,
            category: Category.WerbungskostenFachliteratur,
            priority: 10);

        _dbContext.ClassificationRules.Add(rule);
        await _dbContext.SaveChangesAsync();

        // Vendor matches "Amazon" but description "Laptop" does NOT match "Buch"
        var item = TestDataFactory.CreateReceiptItem(description: "Laptop");

        var result = await _classifier.ClassifyItemAsync(item, vendor: "Amazon.de", sourceFileName: "", userId: TestUserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyItemAsync_RuleMatchedResult_HasCorrectMethodAndStatus()
    {
        var rule = TestDataFactory.CreateRule(
            descriptionPattern: "Stift",
            userId: null,
            category: Category.WerbungskostenBueromaterial,
            priority: 10);

        _dbContext.ClassificationRules.Add(rule);
        await _dbContext.SaveChangesAsync();

        var item = TestDataFactory.CreateReceiptItem(description: "Bleistift");

        var result = await _classifier.ClassifyItemAsync(item, vendor: "", sourceFileName: "", userId: TestUserId);

        result.Should().NotBeNull();
        result!.Method.Should().Be(ClassificationMethod.Rule);
        result.Status.Should().Be(ClassificationStatus.Confirmed);
    }

    public void Dispose() => _dbContext.Dispose();
}
