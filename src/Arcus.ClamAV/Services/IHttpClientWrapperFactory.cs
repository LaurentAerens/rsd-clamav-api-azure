namespace Arcus.ClamAV.Services;

public interface IHttpClientWrapperFactory
{
    IHttpClientWrapper CreateClient();
}
