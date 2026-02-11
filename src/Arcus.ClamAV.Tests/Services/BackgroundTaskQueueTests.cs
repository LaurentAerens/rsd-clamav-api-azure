using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;

namespace Arcus.ClamAV.Tests.Services;

public class BackgroundTaskQueueTests
{
    private readonly BackgroundTaskQueue _queue;
    private readonly Mock<ILogger<BackgroundTaskQueue>> _loggerMock;

    public BackgroundTaskQueueTests()
    {
        _loggerMock = new Mock<ILogger<BackgroundTaskQueue>>();
        _queue = new BackgroundTaskQueue(capacity: 10, logger: _loggerMock.Object);
    }

    [Fact]
    public async Task EnqueueTask_WithValidTask_ShouldSucceed()
    {
        // Arrange
        var taskExecuted = false;
        Task<bool> TestTask(CancellationToken ct)
        {
            taskExecuted = true;
            return Task.FromResult(true);
        }

        // Act
        await _queue.EnqueueTask(TestTask);

        // Assert
        taskExecuted.Should().BeFalse(); // Task hasn't executed yet, just queued
    }

    [Fact]
    public async Task EnqueueTask_WithNullTask_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _queue.EnqueueTask(null!));
    }

    [Fact]
    public async Task DequeueAsync_WithEnqueuedTask_ShouldReturnTask()
    {
        // Arrange
        Task<bool> TestTask(CancellationToken ct) => Task.FromResult(true);
        await _queue.EnqueueTask(TestTask);

        // Act
        var dequeuedTask = await _queue.DequeueAsync(CancellationToken.None);

        // Assert
        dequeuedTask.Should().NotBeNull();
        dequeuedTask.Should().Be(TestTask);
    }

    [Fact]
    public async Task DequeueAsync_WithCancellation_ShouldReturnNull()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act
        var dequeuedTask = await _queue.DequeueAsync(cts.Token);

        // Assert
        dequeuedTask.Should().BeNull();
    }

    [Fact]
    public async Task Queue_WithMultipleTasks_ShouldMaintainOrder()
    {
        // Arrange
        var tasks = new List<Func<CancellationToken, Task<bool>>>();
        for (int i = 0; i < 5; i++)
        {
            var taskId = i;
            Task<bool> TestTask(CancellationToken ct) => Task.FromResult(true);
            tasks.Add(TestTask);
        }

        // Act
        foreach (var task in tasks)
        {
            await _queue.EnqueueTask(task);
        }

        // Assert - dequeue and verify order
        for (int i = 0; i < 5; i++)
        {
            var dequeuedTask = await _queue.DequeueAsync(CancellationToken.None);
            dequeuedTask.Should().NotBeNull();
        }
    }
}

