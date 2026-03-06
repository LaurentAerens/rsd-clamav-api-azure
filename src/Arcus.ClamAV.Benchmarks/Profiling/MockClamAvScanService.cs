using Arcus.ClamAV.Services;
using System.Diagnostics;

namespace Arcus.ClamAV.Benchmarks.Profiling;

/// <summary>
/// Mock ClamAV scan service for benchmarking.
/// Returns instant results without actual ClamAV dependency.
/// Simulates infected detection based on content patterns.
/// </summary>
public class MockClamAvScanService : ISyncScanService
{
    private const string EICAR = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    
    /// <summary>
    /// Scan a stream with minimal latency to isolate JSON extraction performance.
    /// Detects EICAR pattern to simulate infected detection.
    /// </summary>
    public async Task<SyncScanResult> ScanStreamAsync(Stream stream, long size)
    {
        var sw = Stopwatch.StartNew();
        
        // Simulate minimal I/O delay (5ms baseline + 1ms per 100KB)
        var delayMs = 5 + (int)(size / 102_400);
        await Task.Delay(delayMs);

        // Read content to check for EICAR pattern
        var position = stream.Position;
        stream.Seek(0, SeekOrigin.Begin);
        
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        stream.Seek(position, SeekOrigin.Begin);
        sw.Stop();

        // Detect EICAR string in content
        if (content.Contains(EICAR, StringComparison.OrdinalIgnoreCase))
        {
            return new SyncScanResult
            {
                IsSuccess = true,
                Status = "infected",
                Malware = "Eicar-Test-Signature",
                DurationMs = sw.Elapsed.TotalMilliseconds
            };
        }

        return new SyncScanResult
        {
            IsSuccess = true,
            Status = "clean",
            DurationMs = sw.Elapsed.TotalMilliseconds
        };
    }
}
