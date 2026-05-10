using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace TaxReader.UnitTests.Observability;

public class SerilogEnrichmentTests
{
    [Fact]
    public void Config_Loads_FromLogContextEnricher_PropagatesContextProperty()
    {
        var sink = new CapturingSink();
        var logger = BuildLoggerFromAppsettings(sink, environment: "Production");

        using (LogContext.PushProperty("ReceiptFileId", "abc-123"))
        {
            logger.Information("processing");
        }

        sink.Events.Should().HaveCount(1);
        sink.Events[0].Properties.Should().ContainKey("ReceiptFileId");
        sink.Events[0].Properties["ReceiptFileId"].ToString().Should().Contain("abc-123");
    }

    [Fact]
    public void Config_Loads_WithEnvironmentNameEnricher_AttachesEnvironmentName()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "ci-test");
        try
        {
            var sink = new CapturingSink();
            var logger = BuildLoggerFromAppsettings(sink, environment: "ci-test");

            logger.Information("ping");

            sink.Events.Should().HaveCount(1);
            sink.Events[0].Properties.Should().ContainKey("EnvironmentName");
            sink.Events[0].Properties["EnvironmentName"].ToString().Should().Contain("ci-test");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void UploadReceiptFilesHandler_Source_ContainsReceiptFileIdLogContextScope()
    {
        // Structural assertion: the handler must wrap its per-file body in
        // LogContext.PushProperty("ReceiptFileId", receiptFile.Id). Without this
        // scope, OBS-02 cannot meet "Long-running upload handlers emit log lines
        // correlated by ReceiptFileId." The grep is brittle by design — change
        // the literal here only if the wiring legitimately moves.
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Application", "Commands", "UploadReceiptFilesHandler.cs");
        File.Exists(path).Should().BeTrue($"handler not found at {Path.GetFullPath(path)}");

        var source = File.ReadAllText(path);
        source.Should().Contain("using Serilog.Context;");
        source.Should().Contain("using (LogContext.PushProperty(\"ReceiptFileId\", receiptFile.Id))");
    }

    private static ILogger BuildLoggerFromAppsettings(CapturingSink sink, string environment)
    {
        // Locate appsettings.json relative to the test bin directory.
        var appsettings = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Api", "appsettings.json");
        File.Exists(appsettings).Should().BeTrue($"appsettings.json not found at {Path.GetFullPath(appsettings)}");

        var config = new ConfigurationBuilder()
            .AddJsonFile(appsettings, optional: false)
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", environment)
            })
            .Build();

        return new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .WriteTo.Sink(sink)
            .CreateLogger();
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
