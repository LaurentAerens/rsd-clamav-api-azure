using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using Moq;
using nClam;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.Services;

public class SyncScanServiceTests
{
    [Fact]
    public async Task ScanStreamAsync_WithCleanResult_ReturnsCleanSyncResult()
    {
        var mockClamScanService = new Mock<IClamAvScanService>();
        var mockLogger = new Mock<ILogger<SyncScanService>>();
        var stream = new MemoryStream([0x01, 0x02, 0x03]);
        var clamResult = new ClamScanResult("stream: OK");

        mockClamScanService
            .Setup(service => service.ScanFileAsync(stream, stream.Length))
            .ReturnsAsync(clamResult);

        var sut = new SyncScanService(mockClamScanService.Object, mockLogger.Object);

        var result = await sut.ScanStreamAsync(stream, stream.Length);

        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe("clean");
        result.Malware.ShouldBeNull();
        result.Error.ShouldBeNull();
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);

        mockClamScanService.Verify(service => service.ScanFileAsync(stream, stream.Length), Times.Once);
    }

    [Fact]
    public async Task ScanStreamAsync_WithVirusDetectedResult_ReturnsInfectedSyncResult()
    {
        var mockClamScanService = new Mock<IClamAvScanService>();
        var mockLogger = new Mock<ILogger<SyncScanService>>();
        var stream = new MemoryStream([0x04, 0x05]);
        var clamResult = new ClamScanResult("stream: Eicar-Test-Signature FOUND");

        mockClamScanService
            .Setup(service => service.ScanFileAsync(stream, stream.Length))
            .ReturnsAsync(clamResult);

        var sut = new SyncScanService(mockClamScanService.Object, mockLogger.Object);

        var result = await sut.ScanStreamAsync(stream, stream.Length);

        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe("infected");
        result.Malware.ShouldNotBeNullOrWhiteSpace();
        result.Malware.ShouldContain("Eicar");
        result.Error.ShouldBeNull();
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);

        mockClamScanService.Verify(service => service.ScanFileAsync(stream, stream.Length), Times.Once);
    }

    [Fact]
    public async Task ScanStreamAsync_WithNonSuccessScanResult_ReturnsErrorSyncResult()
    {
        var mockClamScanService = new Mock<IClamAvScanService>();
        var mockLogger = new Mock<ILogger<SyncScanService>>();
        var stream = new MemoryStream([0x0A]);
        var clamResult = new ClamScanResult("stream: UNKNOWN");

        mockClamScanService
            .Setup(service => service.ScanFileAsync(stream, stream.Length))
            .ReturnsAsync(clamResult);

        var sut = new SyncScanService(mockClamScanService.Object, mockLogger.Object);

        var result = await sut.ScanStreamAsync(stream, stream.Length);

        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe("error");
        result.Malware.ShouldBeNull();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        result.Error.ShouldContain("Scan error:");
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);

        mockClamScanService.Verify(service => service.ScanFileAsync(stream, stream.Length), Times.Once);
    }

    [Fact]
    public async Task ScanStreamAsync_WhenScanThrows_ReturnsErrorSyncResult()
    {
        var mockClamScanService = new Mock<IClamAvScanService>();
        var mockLogger = new Mock<ILogger<SyncScanService>>();
        var stream = new MemoryStream([0x0B, 0x0C]);

        mockClamScanService
            .Setup(service => service.ScanFileAsync(stream, stream.Length))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = new SyncScanService(mockClamScanService.Object, mockLogger.Object);

        var result = await sut.ScanStreamAsync(stream, stream.Length);

        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe("error");
        result.Malware.ShouldBeNull();
        result.Error.ShouldBe("boom");
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);

        mockClamScanService.Verify(service => service.ScanFileAsync(stream, stream.Length), Times.Once);
    }
}
