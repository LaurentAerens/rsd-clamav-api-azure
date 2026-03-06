using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using System.Text;

namespace Arcus.ClamAV.Benchmarks.Profiling;

/// <summary>
/// Custom BenchmarkDotNet exporter that generates function-level profiling reports
/// </summary>
public class BenchmarkProfilerExporter : IExporter
{
    public string Name => "ProfileReport";
    
    public void ExportToLog(Summary summary, ILogger logger)
    {
        logger.WriteLine();
        logger.WriteLine("═══════════════════════════════════════════════════════════════════");
        logger.WriteLine("  FUNCTION-LEVEL PROFILING REPORT (Averaged Across All Iterations)");
        logger.WriteLine("═══════════════════════════════════════════════════════════════════");
        logger.WriteLine();
        
        if (!BenchmarkProfiler.HasData)
        {
            logger.WriteLine("⚠️  No profiling data was collected during benchmark run.");
            logger.WriteLine("    This may indicate that profiler tracking was not enabled.");
            return;
        }

        var report = BenchmarkProfiler.GenerateReport();
        logger.WriteLine(report.ToText());
        logger.WriteLine();
        
        // Save detailed reports to files
        try
        {
            var resultsDir = summary.ResultsDirectoryPath;
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            
            var txtPath = Path.Combine(resultsDir, $"profile-aggregated-{timestamp}.txt");
            var jsonPath = Path.Combine(resultsDir, $"profile-aggregated-{timestamp}.json");
            var csvPath = Path.Combine(resultsDir, $"profile-aggregated-{timestamp}.csv");
            
            File.WriteAllText(txtPath, report.ToText());
            File.WriteAllText(jsonPath, report.ToJson());
            File.WriteAllText(csvPath, report.ToCsv());
            
            logger.WriteLine("📊 Function-level profiling reports saved:");
            logger.WriteLine($"  • {Path.GetFileName(txtPath)}");
            logger.WriteLine($"  • {Path.GetFileName(jsonPath)}");
            logger.WriteLine($"  • {Path.GetFileName(csvPath)}");
            logger.WriteLine();
        }
        catch (Exception ex)
        {
            logger.WriteLine($"⚠️  Warning: Failed to save profiling reports: {ex.Message}");
        }
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        ExportToLog(summary, consoleLogger);
        return Enumerable.Empty<string>();
    }
}
