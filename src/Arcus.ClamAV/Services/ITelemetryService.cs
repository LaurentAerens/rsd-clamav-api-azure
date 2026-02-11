namespace Arcus.ClamAV.Services;

/// <summary>
/// Interface for tracking custom telemetry metrics and events in Application Insights.
/// Provides business-specific tracking for malware scanning operations.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Tracks a completed scan operation with performance metrics.
    /// </summary>
    /// <param name="scanDurationMs">Duration of the scan in milliseconds</param>
    /// <param name="isClean">True if no malware was detected</param>
    /// <param name="fileSizeBytes">Size of the scanned file in bytes</param>
    /// <param name="scanType">Type of scan performed (file, url, json)</param>
    void TrackScanCompleted(long scanDurationMs, bool isClean, long fileSizeBytes, string scanType);

    /// <summary>
    /// Tracks a malware detection event with threat details.
    /// </summary>
    /// <param name="threatName">Name of the detected threat/virus</param>
    /// <param name="fileName">Name of the infected file</param>
    /// <param name="scanType">Type of scan that detected the malware</param>
    void TrackMalwareDetected(string threatName, string fileName, string scanType);

    /// <summary>
    /// Tracks the current background task queue depth.
    /// </summary>
    /// <param name="queueLength">Number of items in the queue</param>
    void TrackQueueDepth(int queueLength);

    /// <summary>
    /// Tracks background worker utilization metrics.
    /// </summary>
    /// <param name="activeWorkers">Number of currently active workers</param>
    /// <param name="capacity">Maximum worker capacity</param>
    void TrackWorkerUtilization(int activeWorkers, int capacity);

    /// <summary>
    /// Tracks a failed scan operation with error details.
    /// </summary>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="scanType">Type of scan that failed</param>
    /// <param name="exception">Optional exception that caused the failure</param>
    void TrackScanFailed(string errorMessage, string scanType, Exception? exception = null);
}
