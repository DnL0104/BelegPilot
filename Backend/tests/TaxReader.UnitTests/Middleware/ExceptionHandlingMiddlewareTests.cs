using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Api.Middleware;

namespace TaxReader.UnitTests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static DefaultHttpContext MakeContext(CancellationToken requestAborted)
    {
        var ctx = new DefaultHttpContext { RequestAborted = requestAborted };
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/v1/receipt-items/pending-suggestions";
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task ClientAbortedRequest_returns_499_without_logging_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = MakeContext(cts.Token);
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationCanceledException(), logger.Object);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(499);
        VerifyLog(logger, LogLevel.Error, Times.Never());
    }

    [Fact]
    public async Task GenuineException_returns_500_and_logs_error()
    {
        var ctx = MakeContext(CancellationToken.None);
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new Exception("boom"), logger.Object);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
        VerifyLog(logger, LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task CancellationNotFromClient_is_treated_as_a_real_error()
    {
        // OperationCanceledException but the request was NOT aborted by the client
        // (RequestAborted is not cancelled) — e.g. a server-side timeout. Must not be
        // swallowed as a 499; it falls through to the normal error handling.
        var ctx = MakeContext(CancellationToken.None);
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationCanceledException(), logger.Object);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
    }

    private static void VerifyLog(
        Mock<ILogger<ExceptionHandlingMiddleware>> logger, LogLevel level, Times times)
        => logger.Verify(l => l.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);
}
