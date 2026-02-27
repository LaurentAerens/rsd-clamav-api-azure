using System.Text;

namespace Arcus.ClamAV.Services;

public class ClamAvInfoService : IClamAvInfoService
{
    private readonly string _host;
    private readonly int _port;
    private readonly ITcpConnectionFactory _connectionFactory;

    public ClamAvInfoService(IConfiguration config, ITcpConnectionFactory? connectionFactory = null)
    {
        _host = config["CLAMD_HOST"] ?? "127.0.0.1";
        _port = int.TryParse(config["CLAMD_PORT"], out var p) ? p : 3310;
        _connectionFactory = connectionFactory ?? new TcpConnectionFactory();
    }

    public async Task<string> GetVersionAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ConnectAsync(_host, _port);
        using var stream = connection.GetStream();

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

