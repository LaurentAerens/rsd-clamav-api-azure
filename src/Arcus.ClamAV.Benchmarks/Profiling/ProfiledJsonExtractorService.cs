using Arcus.ClamAV.Services;
using System.Text.Json;

namespace Arcus.ClamAV.Benchmarks.Profiling;

/// <summary>
/// JSON extractor service with performance profiling
/// </summary>
public class ProfiledJsonExtractorService : IJsonBase64ExtractorService
{
    private readonly JsonBase64ExtractorService _innerService;
    private readonly PerformanceProfiler _profiler;

    public ProfiledJsonExtractorService(JsonBase64ExtractorService innerService, PerformanceProfiler profiler)
    {
        _innerService = innerService;
        _profiler = profiler;
    }

    public List<Base64Extract> ExtractBase64Properties(JsonElement jsonElement)
    {
        using var _1 = _profiler.TrackMethod("IJsonBase64ExtractorService.ExtractBase64Properties");
        using var _2 = BenchmarkProfiler.TrackMethod("IJsonBase64ExtractorService.ExtractBase64Properties");
        return _innerService.ExtractBase64Properties(jsonElement);
    }

    public bool IsBase64Path(string path)
    {
        return _innerService.IsBase64Path(path);
    }
}
