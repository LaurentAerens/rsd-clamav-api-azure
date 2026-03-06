using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using System.Text.Json;
using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using Arcus.ClamAV.Benchmarks.Profiling;
using Arcus.ClamAV.Benchmarks.TestData;

namespace Arcus.ClamAV.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for the full JSON scan handler pipeline.
/// Tests end-to-end performance: extraction + scanning with mocked ClamAV.
/// </summary>
[Config(typeof(ProfiledBenchmarkConfig))]
public class JsonScanHandlerBenchmarks
{
    private JsonScanHandler _handler = null!;
    private JsonElement _largeCleanPayload;
    private JsonElement _mixedPayload;
    private JsonElement _smallPayload;
    private ILogger<JsonScanHandler> _logger = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Enable global profiling for this benchmark run
        BenchmarkProfiler.Enable();
        
        // Create mock services with minimal logging for benchmarking
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var extractorLogger = loggerFactory.CreateLogger<JsonBase64ExtractorService>();
        var profilerAdapter = new BenchmarkProfilerAdapter();
        
        // Create inner services
        var innerExtractorService = new JsonBase64ExtractorService(extractorLogger, profilerAdapter);
        var innerMockClamService = new MockClamAvScanService();
        
        // Wrap with profiling decorators  
        var extractorService = new ProfiledJsonExtractorService(innerExtractorService, new PerformanceProfiler());
        var syncScanService = new ProfiledMockScanService(innerMockClamService, new PerformanceProfiler());
        
        // Create handler logger
        _logger = loggerFactory.CreateLogger<JsonScanHandler>();

        _handler = new JsonScanHandler(extractorService, syncScanService, _logger, profilerAdapter);

        // Pre-generate test payloads
        _largeCleanPayload = TestPayloadGenerator.CreateLargeCleanPayload();
        _mixedPayload = TestPayloadGenerator.CreateMixedPayload();
        _smallPayload = TestPayloadGenerator.CreateSmallCleanPayload();
    }
    
    [GlobalCleanup]
    public void Cleanup()
    {
        if (!BenchmarkProfiler.HasData) return;
        
        var report = BenchmarkProfiler.GenerateReport();
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  FUNCTION-LEVEL PROFILING REPORT (JsonScanHandlerBenchmarks)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine(report.ToText());
        
        // Save to file
        try
        {
            var resultsDir = BenchmarkArtifacts.GetResultsDirectory();
            Directory.CreateDirectory(resultsDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var baseFileName = $"profile-handler-{timestamp}";
            
            File.WriteAllText(Path.Combine(resultsDir, $"{baseFileName}.txt"), report.ToText());
            File.WriteAllText(Path.Combine(resultsDir, $"{baseFileName}.json"), report.ToJson());
            File.WriteAllText(Path.Combine(resultsDir, $"{baseFileName}.csv"), report.ToCsv());
            
            Console.WriteLine($"📊 Reports saved: {baseFileName}.*");
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Scan small clean payload (fast path, few items to scan).
    /// </summary>
    [Benchmark(Description = "Scan small clean payload (~20KB, 1 base64)")]
    public async Task ScanSmallClean()
    {
        using var _ = BenchmarkProfiler.TrackMethod("JsonScanHandler.HandleAsync [Small]");
        await _handler.HandleAsync(_smallPayload);
    }

    /// <summary>
    /// Scan large clean payload with sparse base64 fields (realistic scenario).
    /// </summary>
    [Benchmark(Description = "Scan large clean payload (~500KB, 3 base64)")]
    public async Task ScanLargeClean()
    {
        using var _ = BenchmarkProfiler.TrackMethod("JsonScanHandler.HandleAsync [Large]");
        await _handler.HandleAsync(_largeCleanPayload);
    }

    /// <summary>
    /// Scan mixed payload with infected base64 (early exit - should be faster).
    /// </summary>
    [Benchmark(Description = "Scan mixed payload (infected found early)")]
    public async Task ScanMixedWithInfected()
    {
        using var _ = BenchmarkProfiler.TrackMethod("JsonScanHandler.HandleAsync [Mixed]");
        await _handler.HandleAsync(_mixedPayload);
    }

    /// <summary>
    /// Measure just the extraction overhead without scanning.
    /// </summary>
    [Benchmark(Description = "Extraction only (no scanning)")]
    public int ExtractOnly_LargePayload()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var logger = loggerFactory.CreateLogger<JsonBase64ExtractorService>();
        var extractorService = new JsonBase64ExtractorService(logger);
        var items = extractorService.ExtractBase64Properties(_largeCleanPayload);
        return items.Count;
    }
}
