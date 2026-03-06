using Arcus.ClamAV.Services;

namespace Arcus.ClamAV.Benchmarks.Profiling;

/// <summary>
/// Mock scan service with performance profiling
/// </summary>
public class ProfiledMockScanService : ISyncScanService
{
    private readonly MockClamAvScanService _innerService;
    private readonly PerformanceProfiler _profiler;

    public ProfiledMockScanService(MockClamAvScanService innerService, PerformanceProfiler profiler)
    {
        _innerService = innerService;
        _profiler = profiler;
    }

    public async Task<SyncScanResult> ScanStreamAsync(Stream stream, long size)
    {
        using var _1 = _profiler.TrackMethod("ISyncScanService.ScanStreamAsync");
        using var _2 = BenchmarkProfiler.TrackMethod("ISyncScanService.ScanStreamAsync");
        return await _innerService.ScanStreamAsync(stream, size);
    }
}
