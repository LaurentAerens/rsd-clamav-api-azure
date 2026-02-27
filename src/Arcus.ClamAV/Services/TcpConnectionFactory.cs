namespace Arcus.ClamAV.Services;

public class TcpConnectionFactory : ITcpConnectionFactory
{
    public ITcpConnection CreateConnection() => new TcpConnection();
}
