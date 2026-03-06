using BenchmarkDotNet.Running;
using Arcus.ClamAV.Benchmarks.Runners;
using System.Diagnostics;

/// <summary>
/// JSON Scanning Performance Benchmarks
/// 
/// Benchmarks the Arcus ClamAV JSON scanning handler to identify performance bottlenecks.
/// Uses BenchmarkDotNet for statistically rigorous measurements.
/// 
/// Results are saved to: BenchmarkDotNet.Artifacts/results-*.md
/// 
/// To run:
///   cd src/Arcus.ClamAV.Benchmarks
///   dotnet run -c Release
/// 
/// Scenarios tested:
///   - JSON base64 extraction performance (large payloads with sparse base64 fields)
///   - Full handler pipeline (extraction + scanning with mocked ClamAV)
///   - Small vs large vs mixed payloads
///   - Clean vs infected detections
/// </summary>
/// 
namespace Arcus.ClamAV.Benchmarks.Runners;

internal class Program
{
    static int Main(string[] args)
    {
        var artifactsRoot = Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts");
        BenchmarkArtifacts.ConfigureArtifactsRoot(artifactsRoot);

        if (args.Any(arg => string.Equals(arg, "--combine-profiles", StringComparison.OrdinalIgnoreCase)))
        {
            return CombineProfiles();
        }

        if (args.Any(arg => string.Equals(arg, "--single-request", StringComparison.OrdinalIgnoreCase)))
        {
            var payloadType = GetArgumentValue(args, "--payload") ?? "large";
            return SingleRequestTraceRunner.Run(payloadType);
        }

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Arcus ClamAV - JSON Scanning Performance Benchmarks      ║");
        Console.WriteLine("║  (ClamAV mocked for isolated C# performance testing)     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This benchmark suite measures:");
        Console.WriteLine("  • JSON base64 extraction performance on large payloads");
        Console.WriteLine("  • Full handler pipeline (extract → scan → respond)");
        Console.WriteLine("  • Memory allocation patterns");
        Console.WriteLine("  • Infection detection (early exit) vs full scan");
        Console.WriteLine();
        Console.WriteLine("Results will be saved to: BenchmarkDotNet.Artifacts/");
        Console.WriteLine();

        var summaries = BenchmarkRunner.Run(typeof(Program).Assembly);

        var resolvedResultsDirectory = Path.GetFullPath(BenchmarkArtifacts.GetResultsDirectory());
        var htmlReports = Directory.Exists(resolvedResultsDirectory)
            ? Directory.GetFiles(resolvedResultsDirectory, "*-report.html", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path)
                .ToArray()
            : Array.Empty<string>();

        Console.WriteLine("\n✅ Benchmarks complete");
        Console.WriteLine($"Results folder: {resolvedResultsDirectory}");

        if (htmlReports.Length > 0)
        {
            Console.WriteLine("HTML reports:");
            foreach (var report in htmlReports)
            {
                Console.WriteLine($"  - {report}");
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = htmlReports[0],
                    UseShellExecute = true
                });
                Console.WriteLine("Opened first HTML report in your default browser.");
            }
            catch
            {
                Console.WriteLine("Could not auto-open browser; open one of the HTML files above manually.");
            }
        }
        else
        {
            Console.WriteLine("No HTML report file was found. Check benchmark output for exporter warnings.");
        }

        CombineProfiles();

        return 0;
    }

    private static int CombineProfiles()
    {
        var resultsDirectory = BenchmarkArtifacts.GetResultsDirectory();
        Directory.CreateDirectory(resultsDirectory);

        var combined = ProfileReportAggregator.CombineReports(resultsDirectory);
        if (combined.SourceFiles.Length == 0)
        {
            Console.WriteLine("No profile JSON files found to combine.");
            return 1;
        }

        var outputPath = Path.Combine(resultsDirectory, $"profile-combined-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(outputPath, combined.ToJson());

        Console.WriteLine("Combined profile report saved:");
        Console.WriteLine($"  - {outputPath}");
        Console.WriteLine($"Included {combined.SourceFiles.Length} source file(s)." );
        return 0;
    }

    private static string? GetArgumentValue(string[] args, string key)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
