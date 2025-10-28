using System.Net.Sockets;
using System.Text;

namespace GovUK.Dfe.ClamAV.Services;

public interface IClamAvInfoService
{
    Task<string> GetVersionAsync();
}

public class ClamAvInfoService : IClamAvInfoService
{
    private readonly string _host;
    private readonly int _port;

    public ClamAvInfoService(IConfiguration config)
    {
        _host = config["CLAMD_HOST"] ?? "127.0.0.1";
        _port = int.TryParse(config["CLAMD_PORT"], out var p) ? p : 3310;
    }

    public async Task<string> GetVersionAsync()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port);
        using var stream = client.GetStream();

        // Send "VERSION\n" command
        var buffer = Encoding.ASCII.GetBytes("VERSION\n");
        await stream.WriteAsync(buffer, 0, buffer.Length);

        // Read response
        var responseBuffer = new byte[512];
        var bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
        var version = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead).Trim();

        return version;
    }
}