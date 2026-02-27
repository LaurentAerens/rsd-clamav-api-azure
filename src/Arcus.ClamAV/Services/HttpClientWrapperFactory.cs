namespace Arcus.ClamAV.Services;

public class HttpClientWrapperFactory : IHttpClientWrapperFactory
{
    public IHttpClientWrapper CreateClient()
    {
        return new HttpClientWrapper();
    }
}
