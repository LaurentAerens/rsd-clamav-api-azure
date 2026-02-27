namespace Arcus.ClamAV.Services;

public interface ITcpConnectionFactory
{
    ITcpConnection CreateConnection();
}
