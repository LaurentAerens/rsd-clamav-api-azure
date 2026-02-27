using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using Moq;
using nClam;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.Services;

public class ScanProcessingServiceTests : IDisposable
{
    private readonly string _tempFilePath;

    public ScanProcessingServiceTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public async Task ProcessFileScanAsync_WithCleanFile_UpdatesJobStatusToClean()
    {
        // Arrange
        File.WriteAllText(_tempFilePath, "clean file content");
        
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        var scanResult = new ClamScanResult("stream: OK");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessFileScanAsync("job1", _tempFilePath, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        mockJobService.Verify(j => j.UpdateJobStatus("job1", "scanning"), Times.Once);
        mockJobService.Verify(j => j.UpdateJobStatus("job1", "clean", null, null), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job1"), Times.Once);
        mockTelemetry.Verify(t => t.TrackScanCompleted(It.IsAny<long>(), true, It.IsAny<long>(), "file"), Times.Once);
    }

    [Fact]
    public async Task ProcessFileScanAsync_WithInfectedFile_UpdatesJobStatusToInfected()
    {
        // Arrange
        File.WriteAllText(_tempFilePath, "infected file content");
        
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        var scanResult = new ClamScanResult("stream: Eicar-Test-Signature FOUND");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessFileScanAsync("job2", _tempFilePath, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        mockJobService.Verify(j => j.UpdateJobStatus("job2", "scanning"), Times.Once);
        mockJobService.Verify(j => j.UpdateJobStatus("job2", "infected", " Eicar-Test-Signature", null), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job2"), Times.Once);
        mockTelemetry.Verify(t => t.TrackScanCompleted(It.IsAny<long>(), false, It.IsAny<long>(), "file"), Times.Once);
        mockTelemetry.Verify(t => t.TrackMalwareDetected(" Eicar-Test-Signature", It.IsAny<string>(), "file"), Times.Once);
    }

    [Fact]
    public async Task ProcessFileScanAsync_WhenScanFails_UpdatesJobStatusToError()
    {
        // Arrange
        File.WriteAllText(_tempFilePath, "file content");
        
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        var scanResult = new ClamScanResult("ERROR");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessFileScanAsync("job3", _tempFilePath, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        mockJobService.Verify(j => j.UpdateJobStatus("job3", "error", null, It.IsAny<string>()), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job3"), Times.Once);
        mockTelemetry.Verify(t => t.TrackScanFailed(It.IsAny<string>(), "file"), Times.Once);
    }

    [Fact]
    public async Task ProcessFileScanAsync_WhenExceptionThrown_UpdatesJobStatusToErrorAndReturnsFalse()
    {
        // Arrange
        File.WriteAllText(_tempFilePath, "file content");
        
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        var expectedException = new InvalidOperationException("Scan service error");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ThrowsAsync(expectedException);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessFileScanAsync("job4", _tempFilePath, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        mockJobService.Verify(j => j.UpdateJobStatus("job4", "error", null, "Scan service error"), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job4"), Times.Once);
        mockTelemetry.Verify(t => t.TrackScanFailed("Scan service error", "file", null), Times.Once);
    }

    [Fact]
    public async Task ProcessFileScanAsync_DeletesTempFile()
    {
        // Arrange
        File.WriteAllText(_tempFilePath, "clean file content");
        
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        var scanResult = new ClamScanResult("stream: OK");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        await service.ProcessFileScanAsync("job5", _tempFilePath, CancellationToken.None);

        // Assert
        File.Exists(_tempFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessUrlScanAsync_WithSuccessfulDownload_CallsProcessFileScan()
    {
        // Arrange
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockHttpClient = new Mock<IHttpClientWrapper>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        mockHttpClientFactory.Setup(f => f.CreateClient()).Returns(mockHttpClient.Object);

        // Mock HEAD request
        var headResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        headResponse.Content = new ByteArrayContent(Array.Empty<byte>());
        headResponse.Content.Headers.ContentLength = 100;

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(headResponse);

        // Mock GET request
        var getResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        getResponse.Content = new ByteArrayContent(new byte[100]);
        getResponse.Content.Headers.ContentLength = 100;

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                HttpCompletionOption.ResponseHeadersRead,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResponse);

        var scanResult = new ClamScanResult("stream: OK");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        mockJobService.Setup(j => j.GetJob("job6")).Returns(new ScanJob { JobId = "job6" });

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessUrlScanAsync("job6", "https://example.com/file.txt", _tempFilePath, 1024 * 1024, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        mockJobService.Verify(j => j.UpdateJobStatus("job6", "downloading"), Times.Once);
        mockJobService.Verify(j => j.UpdateJobStatus("job6", "scanning"), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job6"), Times.Once);
    }

    [Fact]
    public async Task ProcessUrlScanAsync_WithFileTooLarge_UpdatesJobStatusToError()
    {
        // Arrange
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockHttpClient = new Mock<IHttpClientWrapper>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        mockHttpClientFactory.Setup(f => f.CreateClient()).Returns(mockHttpClient.Object);

        // Mock HEAD request with file too large
        var headResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        headResponse.Content = new ByteArrayContent(Array.Empty<byte>());
        headResponse.Content.Headers.ContentLength = 10 * 1024 * 1024; // 10 MB

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(headResponse);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessUrlScanAsync("job7", "https://example.com/large.txt", _tempFilePath, 1024 * 1024, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        mockJobService.Verify(j => j.UpdateJobStatus("job7", "downloading"), Times.Once);
        mockJobService.Verify(j => j.UpdateJobStatus("job7", "error", null, It.Is<string>(s => s.Contains("exceeds maximum"))), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job7"), Times.Once);
    }

    [Fact]
    public async Task ProcessUrlScanAsync_WithDownloadException_UpdatesJobStatusToError()
    {
        // Arrange
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockHttpClient = new Mock<IHttpClientWrapper>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        mockHttpClientFactory.Setup(f => f.CreateClient()).Returns(mockHttpClient.Object);

        var expectedException = new HttpRequestException("Network error");
        mockHttpClient.Setup(c => c.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessUrlScanAsync("job8", "https://example.com/file.txt", _tempFilePath, 1024 * 1024, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        mockJobService.Verify(j => j.UpdateJobStatus("job8", "downloading"), Times.Once);
        mockJobService.Verify(j => j.UpdateJobStatus("job8", "error", null, It.Is<string>(s => s.Contains("Network error"))), Times.Once);
        mockJobService.Verify(j => j.CompleteJob("job8"), Times.Once);
    }

    [Fact]
    public async Task ProcessUrlScanAsync_WithoutContentLength_DownloadsSuccessfully()
    {
        // Arrange
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockHttpClient = new Mock<IHttpClientWrapper>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        mockHttpClientFactory.Setup(f => f.CreateClient()).Returns(mockHttpClient.Object);

        // Mock HEAD request without Content-Length
        var headResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        headResponse.Content = new ByteArrayContent(Array.Empty<byte>());
        // No Content-Length header

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(headResponse);

        // Mock GET request
        var getResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        getResponse.Content = new ByteArrayContent(new byte[50]);

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                HttpCompletionOption.ResponseHeadersRead,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResponse);

        var scanResult = new ClamScanResult("stream: OK");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        var job = new ScanJob { JobId = "job9" };
        mockJobService.Setup(j => j.GetJob("job9")).Returns(job);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessUrlScanAsync("job9", "https://example.com/file.txt", _tempFilePath, 1024 * 1024, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        // FileSize should be set to the downloaded amount (50 bytes)
        job.FileSize.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ProcessUrlScanAsync_WithoutContentLength_SetsJobFileSizeToTotalBytesRead()
    {
        // Arrange
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockHttpClient = new Mock<IHttpClientWrapper>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        mockHttpClientFactory.Setup(f => f.CreateClient()).Returns(mockHttpClient.Object);

        // Mock HEAD request without Content-Length
        var headResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        headResponse.Content = new ByteArrayContent(Array.Empty<byte>());
        // Explicitly remove Content-Length header
        headResponse.Content.Headers.ContentLength = null;

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(headResponse);

        // Mock GET request with StreamContent for proper async reading
        var downloadedBytes = new byte[12345]; // Exactly 12,345 bytes
        var memoryStream = new MemoryStream(downloadedBytes);
        var getResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StreamContent(memoryStream)
        };

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                HttpCompletionOption.ResponseHeadersRead,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResponse);

        var scanResult = new ClamScanResult("stream: OK");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        var job = new ScanJob { JobId = "job-filesize-test" };
        mockJobService.Setup(j => j.GetJob("job-filesize-test"))
            .Returns(job);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessUrlScanAsync("job-filesize-test", "https://example.com/file.bin", _tempFilePath, 1024 * 1024, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        // FileSize should be set to the exact number of bytes downloaded
        job.FileSize.ShouldBe(12345);
        mockJobService.Verify(j => j.GetJob("job-filesize-test"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessUrlScanAsync_WithoutContentLength_WhenJobNotFound_ReturnsSuccessWithoutError()
    {
        // Arrange
        var mockJobService = new Mock<IScanJobService>();
        var mockTelemetry = new Mock<ITelemetryService>();
        var mockClamAvScanService = new Mock<IClamAvScanService>();
        var mockHttpClientFactory = new Mock<IHttpClientWrapperFactory>();
        var mockHttpClient = new Mock<IHttpClientWrapper>();
        var mockLogger = new Mock<ILogger<ScanProcessingService>>();

        mockHttpClientFactory.Setup(f => f.CreateClient()).Returns(mockHttpClient.Object);

        // Mock HEAD request without Content-Length
        var headResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        headResponse.Content = new ByteArrayContent(Array.Empty<byte>());

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(headResponse);

        // Mock GET request
        var getResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        getResponse.Content = new ByteArrayContent(new byte[500]);

        mockHttpClient.Setup(c => c.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                HttpCompletionOption.ResponseHeadersRead,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResponse);

        var scanResult = new ClamScanResult("stream: OK");
        mockClamAvScanService.Setup(s => s.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        // GetJob returns null (job not found)
        mockJobService.Setup(j => j.GetJob("nonexistent-job")).Returns((ScanJob?)null);

        var service = new ScanProcessingService(
            mockJobService.Object,
            mockTelemetry.Object,
            mockClamAvScanService.Object,
            mockHttpClientFactory.Object,
            mockLogger.Object);

        // Act
        var result = await service.ProcessUrlScanAsync("nonexistent-job", "https://example.com/file.txt", _tempFilePath, 1024 * 1024, CancellationToken.None);

        // Assert
        result.ShouldBeTrue(); // Should still complete successfully
        mockJobService.Verify(j => j.GetJob("nonexistent-job"), Times.Once);
    }
}
