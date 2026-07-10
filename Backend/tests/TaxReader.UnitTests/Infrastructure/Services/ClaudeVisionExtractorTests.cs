using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Infrastructure.Services;

public class ClaudeVisionExtractorTests
{
    [Fact]
    public async Task ExtractAsync_WellFormedResponse_ReturnsPopulatedResult()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = "{\"vendor\":\"REWE\",\"items\":[{\"description\":\"Cola\",\"price\":1.79}],\"total\":11.06}"
                }
            }
        });
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var extractor = CreateExtractor(handler, out _);

        using var content = new MemoryStream([1, 2, 3]);
        var result = await extractor.ExtractAsync(content, "image/jpeg", isPdf: false);

        result.Should().NotBeNull();
        result!.Vendor.Should().Be("REWE");
        result.Items.Should().ContainSingle(i => i.Description == "Cola" && i.Price == 1.79m);
        result.Total.Should().Be(11.06m);
    }

    [Fact]
    public async Task ExtractAsync_MarkdownFencedResponseWithLeadingProse_StillParses()
    {
        var innerText = "Hier ist das Ergebnis:\n```json\n" +
            "{\"vendor\":\"TEDI\",\"items\":[{\"description\":\"Bastelbedarf\",\"price\":2.5}],\"total\":2.5}" +
            "\n```\nDanke!";
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = innerText } }
        });
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var extractor = CreateExtractor(handler, out _);

        using var content = new MemoryStream([1, 2, 3]);
        var result = await extractor.ExtractAsync(content, "image/jpeg", isPdf: false);

        result.Should().NotBeNull();
        result!.Vendor.Should().Be("TEDI");
        result.Items.Should().ContainSingle(i => i.Description == "Bastelbedarf" && i.Price == 2.5m);
    }

    [Fact]
    public async Task ExtractAsync_NonJsonGarbage_ReturnsNullAndLogsWarning()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = "Entschuldigung, ich kann das nicht lesen." } }
        });
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var extractor = CreateExtractor(handler, out var logger);

        using var content = new MemoryStream([1, 2, 3]);
        var result = await extractor.ExtractAsync(content, "image/jpeg", isPdf: false);

        result.Should().BeNull();
        VerifyLog(logger, LogLevel.Warning, Times.Once());
    }

    [Fact]
    public void IsConfigured_ApiKeyUnset_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var extractor = CreateExtractor(handler, out _, apiKey: null);

        extractor.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_IsPdfTrue_SendsDocumentContentBlockWithApplicationPdf()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = "{\"vendor\":null,\"items\":[],\"total\":null}" } }
        });
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var extractor = CreateExtractor(handler, out _);

        using var content = new MemoryStream([1, 2, 3]);
        await extractor.ExtractAsync(content, "application/octet-stream", isPdf: true);

        handler.LastRequestBody.Should().NotBeNull();
        handler.LastRequestBody.Should().Contain("\"type\":\"document\"");
        handler.LastRequestBody.Should().Contain("\"media_type\":\"application/pdf\"");
    }

    [Fact]
    public async Task ExtractAsync_IsPdfFalse_SendsImageContentBlockWithPassedMediaType()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = "{\"vendor\":null,\"items\":[],\"total\":null}" } }
        });
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var extractor = CreateExtractor(handler, out _);

        using var content = new MemoryStream([1, 2, 3]);
        await extractor.ExtractAsync(content, "image/jpeg", isPdf: false);

        handler.LastRequestBody.Should().NotBeNull();
        handler.LastRequestBody.Should().Contain("\"type\":\"image\"");
        handler.LastRequestBody.Should().Contain("\"media_type\":\"image/jpeg\"");
    }

    [Fact]
    public async Task ExtractAsync_ServerError_ThrowsHttpRequestException()
    {
        var errorBody = JsonSerializer.Serialize(new
        {
            type = "error",
            error = new { type = "internal_server_error", message = "Something went wrong" }
        });
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, errorBody);
        var extractor = CreateExtractor(handler, out _);

        using var content = new MemoryStream([1, 2, 3]);
        var act = () => extractor.ExtractAsync(content, "image/jpeg", isPdf: false);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static ClaudeVisionExtractor CreateExtractor(
        StubHttpMessageHandler handler,
        out Mock<ILogger<ClaudeVisionExtractor>> logger,
        string? apiKey = "test-key")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var options = Options.Create(new AnthropicOptions { ApiKey = apiKey });
        logger = new Mock<ILogger<ClaudeVisionExtractor>>();
        return new ClaudeVisionExtractor(httpClient, options, logger.Object);
    }

    private static void VerifyLog(
        Mock<ILogger<ClaudeVisionExtractor>> logger, LogLevel level, Times times)
        => logger.Verify(l => l.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    /// <summary>
    /// Stubs the outgoing HTTP call so each test controls the exact response status/body and
    /// can capture the request content for the image-vs-document content-block assertion — no
    /// real network call is made.
    /// </summary>
    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
