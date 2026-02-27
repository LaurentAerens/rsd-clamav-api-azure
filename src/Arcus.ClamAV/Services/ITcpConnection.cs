namespace Arcus.ClamAV.Services;

public interface ITcpConnection : IDisposable
{
    Task ConnectAsync(string host, int port);
    Stream GetStream();
}
