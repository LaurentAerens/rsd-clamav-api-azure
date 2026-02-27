using Arcus.ClamAV.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using nClam;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.Services;

public class ClamAvScanServiceTests
{
    [Fact]
    public async Task ScanFileAsync_WithDefaultConfiguration_UsesDefaultHostAndPort()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        
        mockFactory.Setup(f => f.CreateClient("127.0.0.1", 3310))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 1024);
        
        // Assert
        result.ShouldBe(expectedResult);
        mockFactory.Verify(f => f.CreateClient("127.0.0.1", 3310), Times.Once);
    }
    
    [Fact]
    public async Task ScanFileAsync_WithCustomHost_UsesConfiguredHost()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CLAMD_HOST"] = "custom-host.local"
            }!)
            .Build();
        
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        
        mockFactory.Setup(f => f.CreateClient("custom-host.local", 3310))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 2048);
        
        // Assert
        result.ShouldBe(expectedResult);
        mockFactory.Verify(f => f.CreateClient("custom-host.local", 3310), Times.Once);
    }
    
    [Fact]
    public async Task ScanFileAsync_WithCustomPort_UsesConfiguredPort()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CLAMD_PORT"] = "4567"
            }!)
            .Build();
        
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        
        mockFactory.Setup(f => f.CreateClient("127.0.0.1", 4567))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 4096);
        
        // Assert
        result.ShouldBe(expectedResult);
        mockFactory.Verify(f => f.CreateClient("127.0.0.1", 4567), Times.Once);
    }
    
    [Fact]
    public async Task ScanFileAsync_WithCustomHostAndPort_UsesBothConfiguredValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CLAMD_HOST"] = "scan.example.com",
                ["CLAMD_PORT"] = "9999"
            }!)
            .Build();
        
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        
        mockFactory.Setup(f => f.CreateClient("scan.example.com", 9999))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 8192);
        
        // Assert
        result.ShouldBe(expectedResult);
        mockFactory.Verify(f => f.CreateClient("scan.example.com", 9999), Times.Once);
    }
    
    [Fact]
    public async Task ScanFileAsync_WithInvalidPort_UsesDefaultPort()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CLAMD_PORT"] = "invalid-port"
            }!)
            .Build();
        
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        
        mockFactory.Setup(f => f.CreateClient("127.0.0.1", 3310))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 1024);
        
        // Assert
        result.ShouldBe(expectedResult);
        mockFactory.Verify(f => f.CreateClient("127.0.0.1", 3310), Times.Once);
    }
    
    [Fact]
    public async Task ScanFileAsync_SetsMaxStreamSizeCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        long capturedMaxStreamSize = 0;
        
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockClient.Object);
        mockClient.SetupSet(c => c.MaxStreamSize = It.IsAny<long>())
            .Callback<long>(size => capturedMaxStreamSize = size);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        var fileSize = 123456789L;
        
        // Act
        await service.ScanFileAsync(stream, fileSize);
        
        // Assert
        capturedMaxStreamSize.ShouldBe(fileSize);
    }
    
    [Fact]
    public async Task ScanFileAsync_PassesStreamToClient()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        Stream? capturedStream = null;
        
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .Callback<Stream>(s => capturedStream = s)
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        await service.ScanFileAsync(stream, 1024);
        
        // Assert
        capturedStream.ShouldBe(stream);
    }
    
    [Fact]
    public async Task ScanFileAsync_ReturnsClientScanResult()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("stream: Eicar-Test-Signature FOUND");
        
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 1024);
        
        // Assert
        result.ShouldBe(expectedResult);
        result.Result.ShouldBe(ClamScanResults.VirusDetected);
    }
    
    [Fact]
    public async Task ScanFileAsync_WithCleanFile_ReturnsCleanResult()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("stream: OK");
        
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockClient.Object);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        var result = await service.ScanFileAsync(stream, 1024);
        
        // Assert
        result.ShouldBe(expectedResult);
        result.Result.ShouldBe(ClamScanResults.Clean);
    }
    
    [Fact]
    public async Task ScanFileAsync_WithZeroFileSize_SetsMaxStreamSizeToZero()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var mockFactory = new Mock<IClamClientFactory>();
        var mockClient = new Mock<IClamClientWrapper>();
        var expectedResult = new ClamScanResult("OK");
        long capturedMaxStreamSize = -1;
        
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockClient.Object);
        mockClient.SetupSet(c => c.MaxStreamSize = It.IsAny<long>())
            .Callback<long>(size => capturedMaxStreamSize = size);
        mockClient.Setup(c => c.SendAndScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedResult);
        
        var service = new ClamAvScanService(configuration, mockFactory.Object);
        var stream = new MemoryStream();
        
        // Act
        await service.ScanFileAsync(stream, 0);
        
        // Assert
        capturedMaxStreamSize.ShouldBe(0);
    }
}
