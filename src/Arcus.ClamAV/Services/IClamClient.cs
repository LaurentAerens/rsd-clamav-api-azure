using nClam;

namespace Arcus.ClamAV.Services;

public interface IClamClientWrapper
{
    long MaxStreamSize { get; set; }
    Task<ClamScanResult> SendAndScanFileAsync(Stream stream);
}
