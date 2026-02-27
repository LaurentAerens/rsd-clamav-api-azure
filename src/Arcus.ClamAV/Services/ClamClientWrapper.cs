using nClam;

namespace Arcus.ClamAV.Services;

public class ClamClientWrapper : IClamClientWrapper
{
    private readonly ClamClient _clamClient;

    public ClamClientWrapper(string host, int port)
    {
        _clamClient = new ClamClient(host, port);
    }

    public long MaxStreamSize
    {
        get => _clamClient.MaxStreamSize;
        set => _clamClient.MaxStreamSize = value;
    }

    public Task<ClamScanResult> SendAndScanFileAsync(Stream stream)
    {
        return _clamClient.SendAndScanFileAsync(stream);
    }
}
