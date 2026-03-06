using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Arcus.ClamAV.Benchmarks.Profiling;
using Arcus.ClamAV.Benchmarks.TestData;

namespace Arcus.ClamAV.Benchmarks.Runners;

internal static class SingleRequestTraceRunner
{
    public static int Run(string payloadType)
    {
        try
        {
            var normalizedPayload = payloadType.Trim().ToLowerInvariant();
            var payload = CreatePayload(normalizedPayload);

            Console.WriteLine($"Running single request profile with payload: {normalizedPayload}");
            Console.WriteLine();

            // Create profiler
            var profiler = new PerformanceProfiler();

            // Create mock services (minimal logging)
            var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
            var extractorLogger = loggerFactory.CreateLogger<JsonBase64ExtractorService>();
            var handlerLogger = loggerFactory.CreateLogger<JsonScanHandler>();
            
            var profilerAdapter = new PerformanceProfilerAdapter(profiler);
            var innerExtractorService = new JsonBase64ExtractorService(extractorLogger, profilerAdapter);
            var innerMockClamService = new MockClamAvScanService();
            
            // Wrap services with profiling
            var extractorService = new ProfiledJsonExtractorService(innerExtractorService, profiler);
            var scanService = new ProfiledMockScanService(innerMockClamService, profiler) as ISyncScanService;
            
            var handler = new JsonScanHandler(extractorService, scanService, handlerLogger, profilerAdapter);

            // Execute handler with profiling
            var overallStopwatch = Stopwatch.StartNew();
            
            using (profiler.TrackMethod("JsonScanHandler.HandleAsync"))
            {
                var result = handler.HandleAsync(payload).GetAwaiter().GetResult();
                overallStopwatch.Stop();
                
                Console.WriteLine($"Request completed successfully (result type: {result.GetType().Name})");
            }
            
            Console.WriteLine($"Total execution time: {overallStopwatch.Elapsed.TotalMilliseconds:N2} ms");
            Console.WriteLine();
            
            // Generate and display report
            var report = profiler.GenerateReport();
            Console.WriteLine(report.ToText());
            
            // Save reports to files
            var artifactsDir = BenchmarkArtifacts.GetProfilesDirectory();
            Directory.CreateDirectory(artifactsDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var baseFileName = $"profile-{normalizedPayload}-{timestamp}";
            
            var txtPath = Path.Combine(artifactsDir, $"{baseFileName}.txt");
            var jsonPath = Path.Combine(artifactsDir, $"{baseFileName}.json");
            var csvPath = Path.Combine(artifactsDir, $"{baseFileName}.csv");
            
            File.WriteAllText(txtPath, report.ToText());
            File.WriteAllText(jsonPath, report.ToJson());
            File.WriteAllText(csvPath, report.ToCsv());
            
            Console.WriteLine($"Reports saved to:");
            Console.WriteLine($"  - {txtPath}");
            Console.WriteLine($"  - {jsonPath}");
            Console.WriteLine($"  - {csvPath}");
            
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Single-request trace failed: {exception.Message}");
            return 1;
        }
    }

    private static JsonElement CreatePayload(string payloadType)
    {
        return payloadType switch
        {
            "small" => TestPayloadGenerator.CreateSmallCleanPayload(),
            "mixed" => TestPayloadGenerator.CreateMixedPayload(),
            _ => TestPayloadGenerator.CreateLargeCleanPayload(),
        };
    }
}
