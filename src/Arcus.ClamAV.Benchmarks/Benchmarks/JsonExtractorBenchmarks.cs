using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using System.Text.Json;
using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using Arcus.ClamAV.Benchmarks.Profiling;
using Arcus.ClamAV.Benchmarks.TestData;

namespace Arcus.ClamAV.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for JSON base64 extraction performance.
/// Tests how quickly we can identify and extract base64 fields from large JSON payloads.
/// </summary>
[Config(typeof(ProfiledBenchmarkConfig))]
public class JsonExtractorBenchmarks
{
    private IJsonBase64ExtractorService _extractorService = null!;
    private JsonElement _largeCleanPayload;
    private JsonElement _mixedPayload;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkProfiler.Enable();
        
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var logger = loggerFactory.CreateLogger<JsonBase64ExtractorService>();
        var profilerAdapter = new BenchmarkProfilerAdapter();
        var innerService = new JsonBase64ExtractorService(logger, profilerAdapter);
        _extractorService = new ProfiledJsonExtractorService(innerService, new PerformanceProfiler());
        _largeCleanPayload = TestPayloadGenerator.CreateLargeCleanPayload();
        _mixedPayload = TestPayloadGenerator.CreateMixedPayload();
    }
    
    [GlobalCleanup]
    public void Cleanup()
    {
        if (!BenchmarkProfiler.HasData) return;
        
        var report = BenchmarkProfiler.GenerateReport();
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  FUNCTION-LEVEL PROFILING REPORT (JsonExtractorBenchmarks)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine(report.ToText());
        
        // Save to file
        try
        {
            var resultsDir = BenchmarkArtifacts.GetResultsDirectory();
            Directory.CreateDirectory(resultsDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var baseFileName = $"profile-extractor-{timestamp}";
            
            File.WriteAllText(Path.Combine(resultsDir, $"{baseFileName}.txt"), report.ToText());
            File.WriteAllText(Path.Combine(resultsDir, $"{baseFileName}.json"), report.ToJson());
            File.WriteAllText(Path.Combine(resultsDir, $"{baseFileName}.csv"), report.ToCsv());
            
            Console.WriteLine($"📊 Reports saved: {baseFileName}.*");
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Extract base64 fields from large clean payload (mostly plaintext with sparse base64).
    /// </summary>
    [Benchmark(Description = "Extract base64 from large clean payload (~500KB)")]
    public int ExtractBase64_LargePayload()
    {
        using var _ = BenchmarkProfiler.TrackMethod("ExtractBase64_LargePayload");
        var items = _extractorService.ExtractBase64Properties(_largeCleanPayload);
        return items.Count;
    }

    /// <summary>
    /// Extract base64 fields from mixed clean + infected payload.
    /// </summary>
    [Benchmark(Description = "Extract base64 from mixed clean+infected payload")]
    public int ExtractBase64_MixedPayload()
    {
        using var _ = BenchmarkProfiler.TrackMethod("ExtractBase64_MixedPayload");
        var items = _extractorService.ExtractBase64Properties(_mixedPayload);
        return items.Count;
    }

    /// <summary>
    /// Total time to deserialize and extract from large payload.
    /// </summary>
    [Benchmark(Description = "Full JSON deserialize + extract large payload")]
    public int FullPipeline_LargePayload()
    {
        using var _ = BenchmarkProfiler.TrackMethod("FullPipeline_LargePayload");
        var payload = TestPayloadGenerator.CreateLargeCleanPayload();
        var items = _extractorService.ExtractBase64Properties(payload);
        return items.Count;
    }
}
