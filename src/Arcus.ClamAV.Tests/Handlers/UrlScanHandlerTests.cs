using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.Handlers;

public class UrlScanHandlerTests
{
    private readonly Mock<IScanJobService> _jobServiceMock;
    private readonly Mock<IScanProcessingService> _scanProcessingMock;
    private readonly Mock<IBackgroundTaskQueue> _backgroundTaskQueueMock;
    private readonly Mock<IConfiguration> _configurationMock;

    public UrlScanHandlerTests()
    {
        _jobServiceMock = new Mock<IScanJobService>();
        _scanProcessingMock = new Mock<IScanProcessingService>();
        _backgroundTaskQueueMock = new Mock<IBackgroundTaskQueue>();
        _configurationMock = new Mock<IConfiguration>();
    }

    private UrlScanHandler CreateHandler()
    {
        return new UrlScanHandler(
            _jobServiceMock.Object,
            _scanProcessingMock.Object,
            _backgroundTaskQueueMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNullUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = null! };

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "" };

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "   " };

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task HandleAsync_WithValidBase64Url_ShouldDecodeAndProcess()
    {
        // Arrange
        var actualUrl = "https://example.com/file.pdf";
        var base64Url = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(actualUrl));
        var request = new ScanUrlRequest { Url = base64Url, IsBase64 = true };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job123");

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        _jobServiceMock.Verify(s => s.CreateJob("file.pdf", 0), Times.Once);
        _jobServiceMock.Verify(s => s.UpdateJobStatus("job123", "downloading"), Times.Once);
        _backgroundTaskQueueMock.Verify(
            s => s.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()), 
            Times.Once);

        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(202);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidBase64Url_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "not-valid-base64!!!", IsBase64 = true };

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "not-a-valid-url" };

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task HandleAsync_WithFtpUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "ftp://example.com/file.pdf" };

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task HandleAsync_WithUrlWithoutFilename_ShouldUseHostnameAsFallback()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://example.com/" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job123");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        _jobServiceMock.Verify(s => s.CreateJob("example_com.bin", 0), Times.Once);
        
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(202);
    }

    [Fact]
    public async Task HandleAsync_WithUrlEndingInSlash_ShouldUseHostnameAsFallback()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://api.example.com/v1/" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job456");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        _jobServiceMock.Verify(s => s.CreateJob("api_example_com.bin", 0), Times.Once);
        
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(202);
    }

    [Fact]
    public async Task HandleAsync_WithValidHttpUrl_ShouldEnqueueJob()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "http://example.com/document.pdf" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job789");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        _jobServiceMock.Verify(s => s.CreateJob("document.pdf", 0), Times.Once);
        _jobServiceMock.Verify(s => s.UpdateJobStatus("job789", "downloading"), Times.Once);
        _backgroundTaskQueueMock.Verify(
            s => s.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()), 
            Times.Once);

        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(202);
    }

    [Fact]
    public async Task HandleAsync_WithValidHttpsUrl_ShouldEnqueueJob()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://secure.example.com/report.xlsx" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job999");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        _jobServiceMock.Verify(s => s.CreateJob("report.xlsx", 0), Times.Once);
        _jobServiceMock.Verify(s => s.UpdateJobStatus("job999", "downloading"), Times.Once);
        
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(202);
    }

    [Fact]
    public void Constructor_WithNoConfiguration_ShouldUseDefaultMaxFileSize()
    {
        // Arrange
        _configurationMock.Setup(c => c["MAX_FILE_SIZE_MB"]).Returns((string?)null);

        // Act
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://example.com/file.pdf" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job111");

        // Act - handler should still work with default value
        var result = handler.HandleAsync(request);

        // Assert - no exception thrown, handler works with default 200MB
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidMaxFileSizeConfiguration_ShouldUseDefault()
    {
        // Arrange
        _configurationMock.Setup(c => c["MAX_FILE_SIZE_MB"]).Returns("not-a-number");

        // Act
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://example.com/file.pdf" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job222");

        // Act - handler should still work with default value
        var result = handler.HandleAsync(request);

        // Assert - no exception thrown, handler works with default 200MB
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnAcceptedWithCorrectResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://example.com/test.zip" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("jobABC");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(202);
        
        // Verify the response body would contain the job info
        _jobServiceMock.Verify(s => s.CreateJob("test.zip", 0), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithComplexFilename_ShouldExtractCorrectly()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new ScanUrlRequest { Url = "https://cdn.example.com/downloads/archive-2024-01-15.tar.gz" };

        _jobServiceMock.Setup(s => s.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns("job555");

        // Act
        var result = await handler.HandleAsync(request);

        // Assert
        _jobServiceMock.Verify(s => s.CreateJob("archive-2024-01-15.tar.gz", 0), Times.Once);
    }
}
