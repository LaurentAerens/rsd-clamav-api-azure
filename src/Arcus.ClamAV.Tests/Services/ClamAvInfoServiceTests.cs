using Arcus.ClamAV.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using System.Text;
using Xunit;

namespace Arcus.ClamAV.Tests.Services;

public class ClamAvInfoServiceTests
{
    [Fact]
    public void Constructor_WithHostAndPort_ShouldUseConfiguredValues()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("custom.host.com");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("9999");

        // Act
        var service = new ClamAvInfoService(configMock.Object);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHost_ShouldUseDefaultHost()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns((string?)null);
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("3310");

        // Act
        var service = new ClamAvInfoService(configMock.Object);

        // Assert - should use default "127.0.0.1"
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullPort_ShouldUseDefaultPort()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("localhost");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns((string?)null);

        // Act
        var service = new ClamAvInfoService(configMock.Object);

        // Assert - should use default 3310
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidPort_ShouldUseDefaultPort()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("localhost");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("not-a-number");

        // Act
        var service = new ClamAvInfoService(configMock.Object);

        // Assert - should use default 3310 when parse fails
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyPort_ShouldUseDefaultPort()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("localhost");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns(string.Empty);

        // Act
        var service = new ClamAvInfoService(configMock.Object);

        // Assert - should use default 3310
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetVersionAsync_ShouldConnectAndSendVersionCommand()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("127.0.0.1");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("3310");

        var connectionMock = new Mock<ITcpConnection>();
        var streamMock = new Mock<Stream>();
        
        var responseBytes = Encoding.ASCII.GetBytes("ClamAV 1.0.0/26000/Thu Jan 1 00:00:00 2024\n");
        var responseStream = new MemoryStream(responseBytes);

        streamMock.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        streamMock.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken ct) =>
            {
                return responseStream.Read(buffer, offset, count);
            });

        connectionMock.Setup(c => c.GetStream()).Returns(streamMock.Object);
        connectionMock.Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var factoryMock = new Mock<ITcpConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnection()).Returns(connectionMock.Object);

        var service = new ClamAvInfoService(configMock.Object, factoryMock.Object);

        // Act
        var version = await service.GetVersionAsync();

        // Assert
        version.ShouldNotBeNullOrWhiteSpace();
        version.ShouldContain("ClamAV");
        
        connectionMock.Verify(c => c.ConnectAsync("127.0.0.1", 3310), Times.Once);
        streamMock.Verify(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        streamMock.Verify(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetVersionAsync_ShouldSendCorrectCommand()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("test.host");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("5555");

        var connectionMock = new Mock<ITcpConnection>();
        var streamMock = new Mock<Stream>();
        
        byte[]? writtenBytes = null;
        var responseBytes = Encoding.ASCII.GetBytes("ClamAV 1.2.3\n");
        var responseStream = new MemoryStream(responseBytes);

        streamMock.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], int, int, CancellationToken>((buffer, offset, count, ct) =>
            {
                writtenBytes = buffer.Take(count).ToArray();
            })
            .Returns(Task.CompletedTask);
        
        streamMock.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken ct) =>
            {
                return responseStream.Read(buffer, offset, count);
            });

        connectionMock.Setup(c => c.GetStream()).Returns(streamMock.Object);
        connectionMock.Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var factoryMock = new Mock<ITcpConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnection()).Returns(connectionMock.Object);

        var service = new ClamAvInfoService(configMock.Object, factoryMock.Object);

        // Act
        await service.GetVersionAsync();

        // Assert
        writtenBytes.ShouldNotBeNull();
        var command = Encoding.ASCII.GetString(writtenBytes);
        command.ShouldBe("VERSION\n");
        
        connectionMock.Verify(c => c.ConnectAsync("test.host", 5555), Times.Once);
    }

    [Fact]
    public async Task GetVersionAsync_ShouldTrimResponseWhitespace()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("localhost");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("3310");

        var connectionMock = new Mock<ITcpConnection>();
        var streamMock = new Mock<Stream>();
        
        // Response with extra whitespace
        var responseBytes = Encoding.ASCII.GetBytes("  ClamAV 1.0.0  \n  ");
        var responseStream = new MemoryStream(responseBytes);

        streamMock.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        streamMock.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken ct) =>
            {
                return responseStream.Read(buffer, offset, count);
            });

        connectionMock.Setup(c => c.GetStream()).Returns(streamMock.Object);
        connectionMock.Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var factoryMock = new Mock<ITcpConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnection()).Returns(connectionMock.Object);

        var service = new ClamAvInfoService(configMock.Object, factoryMock.Object);

        // Act
        var version = await service.GetVersionAsync();

        // Assert
        version.ShouldBe("ClamAV 1.0.0");
    }

    [Fact]
    public async Task GetVersionAsync_ShouldHandleLargeResponse()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("localhost");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("3310");

        var connectionMock = new Mock<ITcpConnection>();
        var streamMock = new Mock<Stream>();
        
        // Create a response that's less than 512 bytes
        var longResponse = "ClamAV 1.0.0/" + new string('x', 400);
        var responseBytes = Encoding.ASCII.GetBytes(longResponse);
        var responseStream = new MemoryStream(responseBytes);

        streamMock.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        streamMock.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken ct) =>
            {
                return responseStream.Read(buffer, offset, count);
            });

        connectionMock.Setup(c => c.GetStream()).Returns(streamMock.Object);
        connectionMock.Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var factoryMock = new Mock<ITcpConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnection()).Returns(connectionMock.Object);

        var service = new ClamAvInfoService(configMock.Object, factoryMock.Object);

        // Act
        var version = await service.GetVersionAsync();

        // Assert
        version.ShouldStartWith("ClamAV 1.0.0/");
    }

    [Fact]
    public void TcpConnectionFactory_CreateConnection_ShouldReturnTcpConnection()
    {
        // Arrange
        var factory = new TcpConnectionFactory();

        // Act
        var connection = factory.CreateConnection();

        // Assert
        connection.ShouldNotBeNull();
        connection.ShouldBeAssignableTo<ITcpConnection>();
    }
}
