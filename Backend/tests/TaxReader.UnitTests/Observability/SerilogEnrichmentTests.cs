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
    public void ProcessReceiptFileJob_Source_ContainsJobIdLogContextScope()
    {
        // Structural assertion: Phase 3 moved the per-file LogContext scope from
        // UploadReceiptFilesHandler to ProcessReceiptFileJob (D-05, D-18). The job
        // now wraps its body in LogContext.PushProperty("JobId", receiptFileId) so
        // every log line emitted during processing carries the JobId for correlation.
        // This replaces the old OBS-02 check on the handler (handler is now thin).
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Application", "Jobs", "ProcessReceiptFileJob.cs");
        File.Exists(path).Should().BeTrue($"job not found at {Path.GetFullPath(path)}");

        var source = File.ReadAllText(path);
        source.Should().Contain("LogContext.PushProperty(\"JobId\"",
            "D-05 / D-18: per-file log correlation must be carried by the Hangfire job");
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
