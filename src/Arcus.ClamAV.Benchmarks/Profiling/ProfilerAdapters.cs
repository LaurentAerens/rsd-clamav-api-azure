using Arcus.ClamAV.Services;

namespace Arcus.ClamAV.Benchmarks.Profiling;

internal sealed class PerformanceProfilerAdapter : IPerformanceProfiler
{
    private readonly PerformanceProfiler _profiler;

    public PerformanceProfilerAdapter(PerformanceProfiler profiler)
    {
        _profiler = profiler;
    }

    public IDisposable Track(string name)
    {
        return _profiler.TrackMethod(name);
    }
}

internal sealed class BenchmarkProfilerAdapter : IPerformanceProfiler
{
    public IDisposable Track(string name)
    {
        return BenchmarkProfiler.TrackMethod(name);
    }
}
