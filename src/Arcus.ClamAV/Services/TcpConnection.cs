using System.Net.Sockets;

namespace Arcus.ClamAV.Services;

public class TcpConnection : ITcpConnection
{
    private readonly TcpClient _client;

    public TcpConnection()
    {
        _client = new TcpClient();
    }

    public async Task ConnectAsync(string host, int port)
    {
        await _client.ConnectAsync(host, port);
    }

    public Stream GetStream()
    {
        return _client.GetStream();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
