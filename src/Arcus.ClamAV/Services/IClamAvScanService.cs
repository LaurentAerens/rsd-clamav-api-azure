using nClam;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Interface for ClamAV scanning operations.
/// </summary>
public interface IClamAvScanService
{
    /// <summary>
    /// Scans a file stream asynchronously using ClamAV.
    /// </summary>
    /// <param name="stream">The file stream to scan.</param>
    /// <param name="fileSize">The size of the file.</param>
    /// <returns>The scan result from ClamAV.</returns>
    Task<ClamScanResult> ScanFileAsync(Stream stream, long fileSize);
}
