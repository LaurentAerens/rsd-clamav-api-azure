using nClam;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Implementation of ClamAV scanning using nClam library.
/// </summary>
public class ClamAvScanService(IConfiguration configuration, IClamClientFactory clamClientFactory) : IClamAvScanService
{
    public async Task<ClamScanResult> ScanFileAsync(Stream stream, long fileSize)
    {
        var host = configuration["CLAMD_HOST"] ?? Environment.GetEnvironmentVariable("CLAMD_HOST") ?? "127.0.0.1";
        var port = int.TryParse(configuration["CLAMD_PORT"] ?? Environment.GetEnvironmentVariable("CLAMD_PORT"), out var p) ? p : 3310;

        var clam = clamClientFactory.CreateClient(host, port);
        clam.MaxStreamSize = fileSize;
        return await clam.SendAndScanFileAsync(stream);
    }
}
