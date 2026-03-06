using System.Text.Json;
using Arcus.ClamAV.Benchmarks.Profiling;

namespace Arcus.ClamAV.Benchmarks.Runners;

internal static class ProfileReportAggregator
{
    public static CombinedProfileReport CombineReports(string resultsDirectory)
    {
        var reportFiles = Directory.Exists(resultsDirectory)
            ? Directory.GetFiles(resultsDirectory, "profile-*.json", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        var reports = new List<(string Path, PerformanceReport Report)>();
        foreach (var file in reportFiles)
        {
            var json = File.ReadAllText(file);
            var report = JsonSerializer.Deserialize<PerformanceReport>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (report != null)
            {
                reports.Add((file, report));
            }
        }

        var aggregated = Aggregate(reports.Select(r => r.Report));
        var sources = reports.Select(r => Path.GetFileName(r.Path)).OrderBy(name => name).ToArray();

        return new CombinedProfileReport(
            DateTime.UtcNow,
            resultsDirectory,
            sources,
            aggregated
        );
    }

    private static PerformanceReport Aggregate(IEnumerable<PerformanceReport> reports)
    {
        var methodStats = new Dictionary<string, MethodStats>(StringComparer.Ordinal);
        double totalDurationMs = 0;

        foreach (var report in reports)
        {
            totalDurationMs += report.TotalDurationMs;

            foreach (var method in report.Methods)
            {
                if (!methodStats.TryGetValue(method.MethodName, out var existing))
                {
                    methodStats[method.MethodName] = new MethodStats(
                        method.MethodName,
                        method.CallCount,
                        method.TotalDurationMs,
                        method.AverageDurationMs,
                        method.MinDurationMs,
                        method.MaxDurationMs,
                        0
                    );
                    continue;
                }

                var combinedCallCount = existing.CallCount + method.CallCount;
                var combinedTotal = existing.TotalDurationMs + method.TotalDurationMs;
                var combinedMin = Math.Min(existing.MinDurationMs, method.MinDurationMs);
                var combinedMax = Math.Max(existing.MaxDurationMs, method.MaxDurationMs);
                var combinedAvg = combinedCallCount > 0 ? combinedTotal / combinedCallCount : 0;

                methodStats[method.MethodName] = new MethodStats(
                    method.MethodName,
                    combinedCallCount,
                    combinedTotal,
                    combinedAvg,
                    combinedMin,
                    combinedMax,
                    0
                );
            }
        }

        var methods = methodStats.Values
            .Select(method => method with
            {
                PercentOfTotal = totalDurationMs > 0 ? (method.TotalDurationMs / totalDurationMs * 100) : 0
            })
            .OrderByDescending(method => method.TotalDurationMs)
            .ToList();

        return new PerformanceReport(totalDurationMs, methods);
    }
}

public record CombinedProfileReport(
    DateTime GeneratedAtUtc,
    string ResultsDirectory,
    string[] SourceFiles,
    PerformanceReport AggregatedReport)
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
