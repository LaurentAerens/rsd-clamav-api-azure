namespace Arcus.ClamAV.Services;

public interface IClamClientFactory
{
    IClamClientWrapper CreateClient(string host, int port);
}
