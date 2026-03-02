using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using Microsoft.AspNetCore.Http;

namespace Arcus.ClamAV.Tests.Handlers;

public class FileScanHandlerTests
{
    private readonly Mock<IScanJobService> _jobServiceMock;
    private readonly Mock<IScanProcessingService> _scanProcessingMock;
    private readonly Mock<IBackgroundTaskQueue> _backgroundServiceMock;
    private readonly Mock<ISyncScanService> _syncScanServiceMock;
    private readonly FileScanHandler _handler;

    public FileScanHandlerTests()
    {
        _jobServiceMock = new Mock<IScanJobService>();
        _scanProcessingMock = new Mock<IScanProcessingService>();
        _backgroundServiceMock = new Mock<IBackgroundTaskQueue>();
        _syncScanServiceMock = new Mock<ISyncScanService>();

        _handler = new FileScanHandler(
            _jobServiceMock.Object,
            _scanProcessingMock.Object,
            _backgroundServiceMock.Object,
            _syncScanServiceMock.Object);
    }

    #region HandleSyncAsync Tests

    [Fact]
    public async Task HandleSyncAsync_WithNullFile_ShouldReturnBadRequest()
    {
        // Act
        var result = await _handler.HandleSyncAsync(null!);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleSyncAsync_WithEmptyFile_ShouldReturnBadRequest()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        // Act
        var result = await _handler.HandleSyncAsync(mockFile.Object);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleSyncAsync_WithCleanFile_ShouldCallClamAvScanService()
    {
        // Arrange
        var fileContent = "clean file content"u8.ToArray();
        var mockFile = CreateMockFormFile("test.txt", fileContent);

        var scanResult = new SyncScanResult
        {
            IsSuccess = true,
            Status = "clean",
            Malware = null,
            DurationMs = 5.0
        };
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        // Act
        var result = await _handler.HandleSyncAsync(mockFile);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        
        // Verify the service was called with correct file size
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), (long)fileContent.Length),
            Times.Once);
    }

    [Fact]
    public async Task HandleSyncAsync_ShouldReturnResultFromClamAvService()
    {
        // Arrange
        var fileContent = "test file"u8.ToArray();
        var mockFile = CreateMockFormFile("test.txt", fileContent);

        var scanResult = new SyncScanResult
        {
            IsSuccess = true,
            Status = "clean",
            Malware = null,
            DurationMs = 2.5
        };
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        // Act
        var result = await _handler.HandleSyncAsync(mockFile);

        // Assert
        result.ShouldNotBeNull();
        _syncScanServiceMock.Verify(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()), Times.Once);
    }

    #endregion

    #region HandleAsyncAsync Tests

    [Fact]
    public async Task HandleAsyncAsync_WithNullFile_ShouldReturnBadRequest()
    {
        // Act
        var result = await _handler.HandleAsyncAsync(null!);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsyncAsync_WithEmptyFile_ShouldReturnBadRequest()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        // Act
        var result = await _handler.HandleAsyncAsync(mockFile.Object);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsyncAsync_WithValidFile_ShouldReturnAccepted()
    {
        // Arrange
        var fileContent = "valid file content"u8.ToArray();
        var jobId = Guid.NewGuid().ToString();

        _jobServiceMock.Setup(j => j.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(jobId);

        _backgroundServiceMock.Setup(b => b.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Returns(Task.CompletedTask);

        var mockFile = CreateMockFormFile("document.pdf", fileContent);

        // Act
        var result = await _handler.HandleAsyncAsync(mockFile);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
    }

    [Fact]
    public async Task HandleAsyncAsync_ShouldCreateJobWithFileNameAndSize()
    {
        // Arrange
        var fileName = "testfile.txt";
        var fileContent = "test content"u8.ToArray();
        var jobId = Guid.NewGuid().ToString();

        _jobServiceMock.Setup(j => j.CreateJob(fileName, fileContent.Length))
            .Returns(jobId);

        _backgroundServiceMock.Setup(b => b.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Returns(Task.CompletedTask);

        var mockFile = CreateMockFormFile(fileName, fileContent);

        // Act
        await _handler.HandleAsyncAsync(mockFile);

        // Assert
        _jobServiceMock.Verify(
            j => j.CreateJob(fileName, fileContent.Length),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsyncAsync_ShouldEnqueueBackgroundTaskForProcessing()
    {
        // Arrange
        var fileContent = "test content"u8.ToArray();
        var jobId = Guid.NewGuid().ToString();

        _jobServiceMock.Setup(j => j.CreateJob(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(jobId);

        var backgroundTaskEnqueued = false;
        _backgroundServiceMock.Setup(b => b.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Callback(() => backgroundTaskEnqueued = true)
            .Returns(Task.CompletedTask);

        var mockFile = CreateMockFormFile("test.txt", fileContent);

        // Act
        await _handler.HandleAsyncAsync(mockFile);

        // Assert
        backgroundTaskEnqueued.ShouldBeTrue();
        _backgroundServiceMock.Verify(
            b => b.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsyncAsync_ShouldSaveFileTempDirectory()
    {
        // Arrange
        var fileContent = "test file content for temp save"u8.ToArray();
        var fileName = "testfile.txt";
        var jobId = Guid.NewGuid().ToString();

        _jobServiceMock.Setup(j => j.CreateJob(fileName, fileContent.Length))
            .Returns(jobId);

        _backgroundServiceMock.Setup(b => b.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Returns(Task.CompletedTask);

        var mockFile = CreateMockFormFile(fileName, fileContent);

        // Act
        var result = await _handler.HandleAsyncAsync(mockFile);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status202Accepted);

        // Verify the temp file was created
        var tempPath = Path.Combine(Path.GetTempPath(), $"clamav_{jobId}_{Path.GetFileName(fileName)}");
        File.Exists(tempPath).ShouldBeTrue();

        // Cleanup
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task HandleAsyncAsync_WithFileCopyFailure_ShouldReturnInternalServerError()
    {
        // Arrange
        var fileName = "test.txt";
        var jobId = Guid.NewGuid().ToString();

        _jobServiceMock.Setup(j => j.CreateJob(fileName, 100))
            .Returns(jobId);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(100);
        mockFile.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream("test content"u8.ToArray()));
        
        // Simulate a failure during CopyToAsync
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new IOException("Disk full - cannot write to temp location")));

        // Act
        var result = await _handler.HandleAsyncAsync(mockFile.Object);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult?.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        // Verify job status was updated with error
        _jobServiceMock.Verify(
            j => j.UpdateJobStatus(
                jobId,
                "error",
                null,
                It.IsAny<string>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateMockFormFile(string fileName, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(content));
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>(async (stream, _) =>
            {
                await stream.WriteAsync(content, 0, content.Length);
            })
            .Returns(Task.CompletedTask);

        return mockFile.Object;
    }

    #endregion
}
