using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Arcus.ClamAV.Benchmarks.Profiling;

/// <summary>
/// Lightweight performance profiler that tracks method execution times
/// </summary>
public class PerformanceProfiler
{
    private readonly List<MethodTiming> _timings = new();
    private readonly object _lock = new();

    public IDisposable TrackMethod(string methodName)
    {
        return new MethodTracker(this, methodName);
    }

    private void RecordTiming(string methodName, long elapsedMs)
    {
        lock (_lock)
        {
            _timings.Add(new MethodTiming(methodName, elapsedMs));
        }
    }

    public PerformanceReport GenerateReport()
    {
        lock (_lock)
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
    }

    private class MethodTracker : IDisposable
    {
        private readonly PerformanceProfiler _profiler;
        private readonly string _methodName;
        private readonly Stopwatch _stopwatch;

        public MethodTracker(PerformanceProfiler profiler, string methodName)
        {
            _profiler = profiler;
            _methodName = methodName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _profiler.RecordTiming(_methodName, _stopwatch.ElapsedMilliseconds);
        }
    }
}

public record MethodTiming(string MethodName, long DurationMs);

public record MethodStats(
    string MethodName,
    int CallCount,
    double TotalDurationMs,
    double AverageDurationMs,
    long MinDurationMs,
    long MaxDurationMs,
    double PercentOfTotal
);

public record PerformanceReport(double TotalDurationMs, List<MethodStats> Methods)
{
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Performance Profile Report");
        sb.AppendLine("=========================");
        sb.AppendLine($"Total Execution Time: {TotalDurationMs:N2} ms");
        sb.AppendLine();
        sb.AppendLine($"{"Method",-50} {"Calls",8} {"Total (ms)",12} {"Avg (ms)",12} {"Min (ms)",12} {"Max (ms)",12} {"% Time",8}");
        sb.AppendLine(new string('-', 122));
        
        foreach (var method in Methods)
        {
            sb.AppendLine($"{method.MethodName,-50} {method.CallCount,8} {method.TotalDurationMs,12:N2} {method.AverageDurationMs,12:N2} {method.MinDurationMs,12:N0} {method.MaxDurationMs,12:N0} {method.PercentOfTotal,7:N2}%");
        }
        
        return sb.ToString();
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Method,Calls,TotalMs,AvgMs,MinMs,MaxMs,PercentOfTotal");
        
        foreach (var method in Methods)
        {
            sb.AppendLine($"{EscapeCsv(method.MethodName)},{method.CallCount},{method.TotalDurationMs:N2},{method.AverageDurationMs:N2},{method.MinDurationMs},{method.MaxDurationMs},{method.PercentOfTotal:N2}");
        }
        
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
