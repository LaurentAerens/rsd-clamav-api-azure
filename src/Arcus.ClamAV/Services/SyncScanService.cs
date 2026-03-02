using nClam;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Shared service for synchronous stream scanning with consistent result mapping.
/// Used by both file scan and JSON payload scan handlers.
/// </summary>
public interface ISyncScanService
{
    /// <summary>
    /// Scan a stream and return standardized result.
    /// </summary>
    /// <param name="stream">Stream containing data to scan</param>
    /// <param name="size">Size of stream in bytes</param>
    /// <returns>Standardized scan result</returns>
    Task<SyncScanResult> ScanStreamAsync(Stream stream, long size);
}

/// <summary>
/// Standardized result from a synchronous scan operation.
/// </summary>
public class SyncScanResult
{
    /// <summary>
    /// True if scan completed successfully (clean or infected); false if error.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// "clean", "infected", or "error"
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Malware name if infected, null otherwise.
    /// </summary>
    public string? Malware { get; set; }

    /// <summary>
    /// Error message if IsSuccess is false.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Scan duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }
}

/// <summary>
/// Implementation of ISyncScanService.
/// </summary>
public class SyncScanService(
    IClamAvScanService clamAvScanService,
    ILogger<SyncScanService> logger)
    : ISyncScanService
{
    public async Task<SyncScanResult> ScanStreamAsync(Stream stream, long size)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await clamAvScanService.ScanFileAsync(stream, size);
            var duration = DateTime.UtcNow - startTime;

            return result.Result switch
            {
                ClamScanResults.Clean => new SyncScanResult
                {
                    IsSuccess = true,
                    Status = "clean",
                    Malware = null,
                    DurationMs = duration.TotalMilliseconds
                },
                ClamScanResults.VirusDetected => new SyncScanResult
                {
                    IsSuccess = true,
                    Status = "infected",
                    Malware = result.InfectedFiles?.FirstOrDefault()?.VirusName ?? "unknown",
                    DurationMs = duration.TotalMilliseconds
                },
                _ => new SyncScanResult
                {
                    IsSuccess = false,
                    Status = "error",
                    Error = $"Scan error: {result.RawResult}",
                    Malware = null,
                    DurationMs = duration.TotalMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            logger.LogError(ex, "Error scanning stream");
            return new SyncScanResult
            {
                IsSuccess = false,
                Status = "error",
                Error = ex.Message,
                Malware = null,
                DurationMs = duration.TotalMilliseconds
            };
        }
    }
}
