using System.Collections.Concurrent;
using System.Diagnostics;

namespace Arcus.ClamAV.Benchmarks.Profiling;

/// <summary>
/// Global profiler that aggregates timing data across all benchmark iterations
/// </summary>
public static class BenchmarkProfiler
{
    private static readonly ConcurrentBag<MethodTiming> _timings = new();
    private static bool _enabled = false;

    public static bool IsEnabled => _enabled;
    public static bool HasData => !_timings.IsEmpty;

    public static void Enable()
    {
        _enabled = true;
        _timings.Clear();
    }

    public static void Disable()
    {
        _enabled = false;
    }

    public static void Reset()
    {
        _timings.Clear();
    }

    public static IDisposable TrackMethod(string methodName)
    {
        if (!_enabled) return NoOpTracker.Instance;
        return new MethodTracker(methodName);
    }

    private static void RecordTiming(string methodName, long elapsedMs)
    {
        if (_enabled)
        {
            _timings.Add(new MethodTiming(methodName, elapsedMs));
        }
    }

    public static PerformanceReport GenerateReport()
    {
        var timings = _timings.ToList();
        var totalTime = (double)timings.Sum(t => t.DurationMs);
        
        var methodGroups = timings
            .GroupBy(t => t.MethodName)
            .Select(g => new MethodStats(
                g.Key,
                g.Count(),
                (double)g.Sum(t => t.DurationMs),
                g.Average(t => t.DurationMs),
                g.Min(t => t.DurationMs),
                g.Max(t => t.DurationMs),
                totalTime > 0 ? ((double)g.Sum(t => t.DurationMs) / totalTime * 100) : 0
            ))
            .OrderByDescending(s => s.TotalDurationMs)
            .ToList();

        return new PerformanceReport(totalTime, methodGroups);
    }

    private class MethodTracker : IDisposable
    {
        private readonly string _methodName;
        private readonly Stopwatch _stopwatch;

        public MethodTracker(string methodName)
        {
            _methodName = methodName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            RecordTiming(_methodName, _stopwatch.ElapsedMilliseconds);
        }
    }

    private class NoOpTracker : IDisposable
    {
        public static readonly NoOpTracker Instance = new();
        public void Dispose() { }
    }
}
