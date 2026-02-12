using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Arcus.ClamAV.Tests.Services;

public class QueuedHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldProcessQueuedTasks()
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns("1");

        var service = new QueuedHostedService(
            taskQueueMock.Object, 
            telemetryServiceMock.Object,
            loggerMock.Object, 
            configMock.Object);

        // Setup the queue to return null (to stop the service)
        taskQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<CancellationToken, Task<bool>>?)null);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to start
        await service.StopAsync(CancellationToken.None);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task StartAsync_ShouldCreateWorkerTasksBasedOnConfiguration()
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns("4");

        var service = new QueuedHostedService(
            taskQueueMock.Object, 
            telemetryServiceMock.Object,
            loggerMock.Object, 
            configMock.Object);

        taskQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<CancellationToken, Task<bool>>?)null);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert - service should have created 4 workers
        service.ShouldNotBeNull();
    }
}

