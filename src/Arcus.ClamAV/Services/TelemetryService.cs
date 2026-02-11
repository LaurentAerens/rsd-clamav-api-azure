using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Implementation of telemetry tracking service using Application Insights.
/// Safe to use even when Application Insights is not configured (methods become no-ops).
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(ILogger<TelemetryService> logger, TelemetryClient? telemetryClient = null)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    /// <inheritdoc />
    public void TrackScanCompleted(long scanDurationMs, bool isClean, long fileSizeBytes, string scanType)
    {
        if (_telemetryClient == null) return;

        try
        {
            // Track scan duration as a custom metric
            _telemetryClient.TrackMetric(
                "ScanDuration",
                scanDurationMs,
                new Dictionary<string, string>
                {
                    { "ScanType", scanType },
                    { "IsClean", isClean.ToString() },
                    { "FileSizeCategory", GetFileSizeCategory(fileSizeBytes) }
                });

            // Track scan throughput (files scanned)
            _telemetryClient.TrackMetric(
                "ScansCompleted",
                1,
                new Dictionary<string, string>
                {
                    { "ScanType", scanType },
                    { "Result", isClean ? "Clean" : "Infected" }
                });

            // Track file size distribution
            _telemetryClient.TrackMetric(
                "FileSizeScanned",
                fileSizeBytes / 1024.0, // Convert to KB
                new Dictionary<string, string>
                {
                    { "ScanType", scanType }
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track scan completion telemetry");
        }
    }

    /// <inheritdoc />
    public void TrackMalwareDetected(string threatName, string fileName, string scanType)
    {
        if (_telemetryClient == null) return;

        try
        {
            // Track as custom event with threat details
            var properties = new Dictionary<string, string>
            {
                { "ThreatName", threatName },
                { "FileName", fileName },
                { "ScanType", scanType },
                { "Timestamp", DateTimeOffset.UtcNow.ToString("o") }
            };

            _telemetryClient.TrackEvent("MalwareDetected", properties);

            // Track malware detection count metric
            _telemetryClient.TrackMetric(
                "MalwareDetections",
                1,
                new Dictionary<string, string>
                {
                    { "ScanType", scanType },
                    { "ThreatCategory", GetThreatCategory(threatName) }
                });

            _logger.LogWarning("Malware detected and tracked: {ThreatName} in {FileName}", threatName, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track malware detection telemetry");
        }
    }

    /// <inheritdoc />
    public void TrackQueueDepth(int queueLength)
    {
        if (_telemetryClient == null) return;

        try
        {
            _telemetryClient.TrackMetric(
                "BackgroundQueueDepth",
                queueLength);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to track queue depth telemetry");
        }
    }

    /// <inheritdoc />
    public void TrackWorkerUtilization(int activeWorkers, int capacity)
    {
        if (_telemetryClient == null) return;

        try
        {
            // Track active workers
            _telemetryClient.TrackMetric("ActiveWorkers", activeWorkers);

            // Track utilization percentage
            var utilizationPercent = capacity > 0 ? (activeWorkers * 100.0 / capacity) : 0;
            _telemetryClient.TrackMetric(
                "WorkerUtilization",
                utilizationPercent,
                new Dictionary<string, string>
                {
                    { "Capacity", capacity.ToString() }
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to track worker utilization telemetry");
        }
    }

    /// <inheritdoc />
    public void TrackScanFailed(string errorMessage, string scanType, Exception? exception = null)
    {
        if (_telemetryClient == null) return;

        try
        {
            var properties = new Dictionary<string, string>
            {
                { "ErrorMessage", errorMessage },
                { "ScanType", scanType }
            };

            if (exception != null)
            {
                properties["ExceptionType"] = exception.GetType().Name;
                properties["ExceptionMessage"] = exception.Message;
            }

            _telemetryClient.TrackEvent("ScanFailed", properties);

            // Track failure metric
            _telemetryClient.TrackMetric(
                "ScanFailures",
                1,
                new Dictionary<string, string>
                {
                    { "ScanType", scanType },
                    { "ErrorCategory", GetErrorCategory(errorMessage) }
                });

            // Track as exception if provided
            if (exception != null)
            {
                _telemetryClient.TrackException(exception, properties);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track scan failure telemetry");
        }
    }

    /// <summary>
    /// Categorizes file size for better metric aggregation.
    /// </summary>
    private static string GetFileSizeCategory(long fileSizeBytes)
    {
        var sizeInMb = fileSizeBytes / (1024.0 * 1024.0);

        return sizeInMb switch
        {
            < 1 => "Small (<1MB)",
            < 10 => "Medium (1-10MB)",
            < 50 => "Large (10-50MB)",
            < 100 => "VeryLarge (50-100MB)",
            _ => "Huge (>100MB)"
        };
    }

    /// <summary>
    /// Categorizes threat type from threat name.
    /// </summary>
    private static string GetThreatCategory(string threatName)
    {
        var lowerThreat = threatName.ToLowerInvariant();

        if (lowerThreat.Contains("eicar")) return "Test";
        if (lowerThreat.Contains("trojan")) return "Trojan";
        if (lowerThreat.Contains("virus")) return "Virus";
        if (lowerThreat.Contains("worm")) return "Worm";
        if (lowerThreat.Contains("ransomware")) return "Ransomware";
        if (lowerThreat.Contains("adware") || lowerThreat.Contains("spyware")) return "Spyware";
        if (lowerThreat.Contains("rootkit")) return "Rootkit";

        return "Other";
    }

    /// <summary>
    /// Categorizes error type for better metric aggregation.
    /// </summary>
    private static string GetErrorCategory(string errorMessage)
    {
        var lowerError = errorMessage.ToLowerInvariant();

        if (lowerError.Contains("timeout")) return "Timeout";
        if (lowerError.Contains("connection") || lowerError.Contains("network")) return "Network";
        if (lowerError.Contains("file") || lowerError.Contains("path")) return "FileSystem";
        if (lowerError.Contains("memory") || lowerError.Contains("oom")) return "Memory";
        if (lowerError.Contains("permission") || lowerError.Contains("access denied")) return "Permission";
        if (lowerError.Contains("parse") || lowerError.Contains("format")) return "Format";

        return "Other";
    }
}
