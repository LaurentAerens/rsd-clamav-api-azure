namespace Arcus.ClamAV.Services;

public class ClamClientFactory : IClamClientFactory
{
    public IClamClientWrapper CreateClient(string host, int port)
    {
        return new ClamClientWrapper(host, port);
    }
}
