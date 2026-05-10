using FluentAssertions;
using Sentry;
using TaxReader.Infrastructure.Observability;

namespace TaxReader.UnitTests.Observability;

public class SentryScrubbingTests
{
    [Fact]
    public void Scrub_RequestBody_StrippedToNull()
    {
        var ev = new SentryEvent
        {
            Request = new SentryRequest { Data = "{ \"vendor\": \"Amazon EU\" }" }
        };

        var result = SentryScrubbing.Scrub(ev);

        result.Should().NotBeNull();
        result!.Request!.Data.Should().BeNull();
    }

    [Theory]
    [InlineData("page=1&secret=foo&year=2025", "page=1", "year=2025")]
    [InlineData("pageSize=10&token=bad", "pageSize=10")]
    [InlineData("format=csv&authorization=ohno", "format=csv")]
    public void Scrub_QueryString_AllowsOnlyPagePageSizeYearFormat(string input, params string[] expectedFragments)
    {
        var ev = new SentryEvent
        {
            Request = new SentryRequest { QueryString = input }
        };

        var result = SentryScrubbing.Scrub(ev);

        foreach (var frag in expectedFragments)
        {
            result!.Request!.QueryString.Should().Contain(frag);
        }
        result!.Request!.QueryString.Should().NotContain("secret");
        result!.Request!.QueryString.Should().NotContain("token");
        result!.Request!.QueryString.Should().NotContain("authorization");
    }

    [Fact]
    public void Scrub_Headers_AllowsOnlyUserAgent()
    {
        var ev = new SentryEvent { Request = new SentryRequest() };
        ev.Request!.Headers!.Add("User-Agent", "Mozilla/5.0");
        ev.Request!.Headers!.Add("Authorization", "Bearer secret");
        ev.Request!.Headers!.Add("Cookie", "session=abc");

        var result = SentryScrubbing.Scrub(ev);

        result!.Request!.Headers.Should().ContainKey("User-Agent");
        result!.Request!.Headers.Should().NotContainKey("Authorization");
        result!.Request!.Headers.Should().NotContainKey("Cookie");
    }

    [Theory]
    [InlineData(
        "https://api.taxreader.de/api/v1/receipts/3f1c8b22-7b1f-4f1a-9a2e-aabbccddeeff/items",
        "https://api.taxreader.de/api/v1/receipts/:id/items")]
    [InlineData(
        "https://api/receipts/3F1C8B22-7B1F-4F1A-9A2E-AABBCCDDEEFF",
        "https://api/receipts/:id")]
    public void Scrub_UrlWithUuid_MaskedToColonId(string input, string expected)
    {
        var ev = new SentryEvent { Request = new SentryRequest { Url = input } };
        var result = SentryScrubbing.Scrub(ev);
        result!.Request!.Url.Should().Be(expected);
    }

    [Fact]
    public void Scrub_User_EmailDroppedIdHashed()
    {
        var ev = new SentryEvent
        {
            User = new SentryUser
            {
                Email = "user@example.com",
                Username = "alice",
                IpAddress = "1.2.3.4",
                Id = "user-id-1234"
            }
        };

        var result = SentryScrubbing.Scrub(ev);

        result!.User.Email.Should().BeNull();
        result!.User.Username.Should().BeNull();
        result!.User.IpAddress.Should().BeNull();
        result!.User.Id.Should().BeNull();
        result!.User.Other.Should().ContainKey("id_hash");
        result!.User.Other!["id_hash"].Should().HaveLength(16);
        // Determinism: same input → same hash.
        result!.User.Other!["id_hash"].Should()
            .Be(SentryScrubbing.HashUserId("user-id-1234"));
    }

    [Fact]
    public void Scrub_RawReceiptContentInExtras_NeverSet()
    {
        // D-14 #6 — active enforcement: the scrubber drops Extra keys not in the
        // small allow-list, so even if a future contributor erroneously calls
        // Sentry.SetExtra("vendor", receipt.Vendor), the event never leaves the
        // process with that data. Allowed keys (receipt_id, processing_run_id,
        // request_id, job_id, phase) survive — they're non-PII identifiers.
        var ev = new SentryEvent();
        ev.SetExtra("vendor", "Amazon EU GmbH");
        ev.SetExtra("item_description", "Lehrbuch fuer Mathematik 2025");
        ev.SetExtra("reasoning", "Diese Position passt zu Fachliteratur weil...");
        ev.SetExtra("receipt_total", 49.95m);
        ev.SetExtra("customer_email", "lehrer@example.de");
        ev.SetExtra("receipt_id", Guid.NewGuid().ToString());     // allowed
        ev.SetExtra("processing_run_id", Guid.NewGuid().ToString()); // allowed

        var result = SentryScrubbing.Scrub(ev);

        result!.Extra.Should().NotContainKey("vendor");
        result!.Extra.Should().NotContainKey("item_description");
        result!.Extra.Should().NotContainKey("reasoning");
        result!.Extra.Should().NotContainKey("receipt_total");
        result!.Extra.Should().NotContainKey("customer_email");
        result!.Extra.Should().ContainKey("receipt_id");
        result!.Extra.Should().ContainKey("processing_run_id");
    }

    [Fact]
    public void Scrub_KitchenSink_AppliesAllRules()
    {
        var ev = new SentryEvent
        {
            Request = new SentryRequest
            {
                Data = "{ secret data }",
                QueryString = "page=1&secret=foo",
                Url = "https://api/receipts/3f1c8b22-7b1f-4f1a-9a2e-aabbccddeeff"
            },
            User = new SentryUser { Email = "u@example.com", Id = "uid" }
        };
        ev.Request!.Headers!.Add("Authorization", "Bearer y");
        ev.Request!.Headers!.Add("User-Agent", "x");
        ev.SetExtra("vendor", "Amazon EU");                  // disallowed
        ev.SetExtra("receipt_id", "abc-123");                // allowed

        var result = SentryScrubbing.Scrub(ev);

        result!.Request!.Data.Should().BeNull();
        result!.Request!.QueryString.Should().Be("page=1");
        result!.Request!.Url.Should().EndWith(":id");
        result!.Request!.Headers.Should().ContainKey("User-Agent");
        result!.Request!.Headers.Should().NotContainKey("Authorization");
        result!.User.Email.Should().BeNull();
        result!.User.Other.Should().ContainKey("id_hash");
        result!.Extra.Should().NotContainKey("vendor");
        result!.Extra.Should().ContainKey("receipt_id");
    }
}
