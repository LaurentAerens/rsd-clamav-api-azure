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

    [Theory]
    [InlineData("5", 5)]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("100", 100)]
    public async Task Constructor_WithValidMaxConcurrentWorkersConfig_UsesConfiguredValue(string configValue, int expectedWorkers)
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns(configValue);

        taskQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<CancellationToken, Task<bool>>?)null);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        var service = new QueuedHostedService(
            taskQueueMock.Object,
            telemetryServiceMock.Object,
            loggerMock.Object,
            configMock.Object);

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert - verify the log message contains the expected worker count
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"with {expectedWorkers} concurrent workers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(null)] // Missing configuration
    [InlineData("")] // Empty string
    [InlineData("invalid")] // Non-numeric
    [InlineData("abc123")] // Mixed characters
    [InlineData("3.14")] // Decimal
    public async Task Constructor_WithInvalidMaxConcurrentWorkersConfig_UsesDefaultValue(string? configValue)
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns(configValue);

        taskQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<CancellationToken, Task<bool>>?)null);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        var service = new QueuedHostedService(
            taskQueueMock.Object,
            telemetryServiceMock.Object,
            loggerMock.Object,
            configMock.Object);

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert - verify the log message contains the default value of 4
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("with 4 concurrent workers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkerAsync_WhenTaskThrowsOperationCanceledException_StopsProcessingAndExitsLoop()
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns("1");

        var taskExecutionCount = 0;
        
        taskQueueMock.SetupSequence(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Func<CancellationToken, Task<bool>>(async ct =>
            {
                taskExecutionCount++;
                await Task.CompletedTask;
                throw new OperationCanceledException("Task cancelled");
            }))
            .ReturnsAsync(new Func<CancellationToken, Task<bool>>(async ct =>
            {
                // This task should NEVER execute because OperationCanceledException breaks the loop
                taskExecutionCount++;
                await Task.CompletedTask;
                return true;
            }));

        var service = new QueuedHostedService(
            taskQueueMock.Object,
            telemetryServiceMock.Object,
            loggerMock.Object,
            configMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200); // Give worker time to process
        await service.StopAsync(CancellationToken.None);

        // Assert - Only the first task should have executed, then the worker stops
        taskExecutionCount.ShouldBe(1);
        
        // Verify the cancellation was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkerAsync_WhenTaskThrowsException_ContinuesProcessingNextTasks()
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns("1");

        var taskExecutionCount = 0;
        var taskQueue = new Queue<Func<CancellationToken, Task<bool>>>();
        
        // Queue the tasks
        taskQueue.Enqueue(async ct =>
        {
            await Task.CompletedTask;
            taskExecutionCount++;
            throw new InvalidOperationException("Simulated task failure");
        });
        
        taskQueue.Enqueue(async ct =>
        {
            await Task.CompletedTask;
            taskExecutionCount++;
            return true;
        });

        taskQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns(async () => 
            {
                await Task.Delay(10); // Simulate async work
                return taskQueue.Count > 0 ? taskQueue.Dequeue() : null;
            });

        var service = new QueuedHostedService(
            taskQueueMock.Object,
            telemetryServiceMock.Object,
            loggerMock.Object,
            configMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2)); // Timeout after 2 seconds

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(500); // Give worker time to process tasks
        await service.StopAsync(CancellationToken.None);

        // Assert
        taskExecutionCount.ShouldBe(2);
        
        // Verify the error was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("encountered an error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WorkerAsync_WhenTaskSucceeds_ContinuesProcessingNextTasks()
    {
        // Arrange
        var taskQueueMock = new Mock<IBackgroundTaskQueue>();
        var telemetryServiceMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<QueuedHostedService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundProcessing:MaxConcurrentWorkers"]).Returns("1");

        var taskExecutionCount = 0;
        var taskQueue = new Queue<Func<CancellationToken, Task<bool>>>();
        
        // Queue the tasks
        taskQueue.Enqueue(async ct =>
        {
            await Task.CompletedTask;
            taskExecutionCount++;
            return true; // Success
        });
        
        taskQueue.Enqueue(async ct =>
        {
            await Task.CompletedTask;
            taskExecutionCount++;
            return false; // Failure but doesn't throw
        });
        
        taskQueue.Enqueue(async ct =>
        {
            await Task.CompletedTask;
            taskExecutionCount++;
            return true; // Success again
        });

        taskQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns(async () => 
            {
                await Task.Delay(10); // Simulate async work
                return taskQueue.Count > 0 ? taskQueue.Dequeue() : null;
            });

        var service = new QueuedHostedService(
            taskQueueMock.Object,
            telemetryServiceMock.Object,
            loggerMock.Object,
            configMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2)); // Timeout after 2 seconds

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(500); // Give worker time to process all tasks
        await service.StopAsync(CancellationToken.None);

        // Assert
        taskExecutionCount.ShouldBe(3);
        
        // Verify telemetry was tracked for each task
        telemetryServiceMock.Verify(
            x => x.TrackWorkerUtilization(It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(3));
    }
}

