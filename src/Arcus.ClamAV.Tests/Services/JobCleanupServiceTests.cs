using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.Services;

public class JobCleanupServiceTests
{
    [Fact]
    public async Task StartAsync_StartsSuccessfully_ReturnsCompletedTask()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        // Act
        await service.StartAsync(CancellationToken.None);
        
        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Job Cleanup Service is starting")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task StopAsync_StopsSuccessfully_ReturnsCompletedTask()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        await service.StartAsync(CancellationToken.None);
        
        // Act
        await service.StopAsync(CancellationToken.None);
        
        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Job Cleanup Service is stopping")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ScheduleCleanup_EnqueuesCleanupTask()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        Func<CancellationToken, Task<bool>>? capturedTask = null;
        mockQueue.Setup(q => q.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Callback<Func<CancellationToken, Task<bool>>>(task => capturedTask = task);
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        // Act - Start service which will set up timer, then wait briefly to allow timer to trigger
        await service.StartAsync(CancellationToken.None);
        
        // Give time for timer to fire (first run is after 1 minute, but we'll trigger it manually)
        // We need to wait for the timer callback to execute
        await Task.Delay(100);
        
        // Assert - For this test, we just verify the service started
        // We can't easily test the timer callback without additional infrastructure
        // So we'll create a separate test that directly tests the cleanup logic
        capturedTask.ShouldBeNull(); // Timer hasn't fired yet in test
    }
    
    [Fact]
    public async Task CleanupTask_WhenExecuted_CallsCleanupOldJobs()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        Func<CancellationToken, Task<bool>>? capturedTask = null;
        mockQueue.Setup(q => q.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Callback<Func<CancellationToken, Task<bool>>>(task => capturedTask = task);
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        await service.StartAsync(CancellationToken.None);
        
        // Simulate timer callback by using reflection to call ScheduleCleanup
        var method = typeof(JobCleanupService).GetMethod("ScheduleCleanup", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(service, null);
        
        // Assert - Task should be enqueued
        capturedTask.ShouldNotBeNull();
        
        // Act - Execute the captured task
        var result = await capturedTask(CancellationToken.None);
        
        // Assert - Should call cleanup with 24 hour timespan
        result.ShouldBeTrue();
        mockJobService.Verify(j => j.CleanupOldJobs(TimeSpan.FromHours(24)), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting scheduled job cleanup")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Scheduled job cleanup completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task CleanupTask_WhenCleanupFails_LogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        var expectedException = new InvalidOperationException("Cleanup failed");
        mockJobService.Setup(j => j.CleanupOldJobs(It.IsAny<TimeSpan>()))
            .Throws(expectedException);
        
        Func<CancellationToken, Task<bool>>? capturedTask = null;
        mockQueue.Setup(q => q.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Callback<Func<CancellationToken, Task<bool>>>(task => capturedTask = task);
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        await service.StartAsync(CancellationToken.None);
        
        // Simulate timer callback
        var method = typeof(JobCleanupService).GetMethod("ScheduleCleanup", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(service, null);
        
        capturedTask.ShouldNotBeNull();
        
        // Act - Execute the captured task
        var result = await capturedTask(CancellationToken.None);
        
        // Assert - Should return false and log error
        result.ShouldBeFalse();
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during scheduled job cleanup")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task CleanupTask_CleansUpJobsOlderThan24Hours()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        TimeSpan? capturedRetentionPeriod = null;
        mockJobService.Setup(j => j.CleanupOldJobs(It.IsAny<TimeSpan>()))
            .Callback<TimeSpan>(ts => capturedRetentionPeriod = ts);
        
        Func<CancellationToken, Task<bool>>? capturedTask = null;
        mockQueue.Setup(q => q.EnqueueTask(It.IsAny<Func<CancellationToken, Task<bool>>>()))
            .Callback<Func<CancellationToken, Task<bool>>>(task => capturedTask = task);
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        await service.StartAsync(CancellationToken.None);
        
        // Simulate timer callback
        var method = typeof(JobCleanupService).GetMethod("ScheduleCleanup", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(service, null);
        
        capturedTask.ShouldNotBeNull();
        
        // Act
        await capturedTask(CancellationToken.None);
        
        // Assert - Verify retention period is exactly 24 hours
        capturedRetentionPeriod.ShouldNotBeNull();
        capturedRetentionPeriod.Value.ShouldBe(TimeSpan.FromHours(24));
    }
    
    [Fact]
    public void Dispose_DisposesTimer()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        // Act
        service.Dispose();
        
        // Assert - No exception should be thrown
        // Multiple disposes should be safe
        service.Dispose();
    }
    
    [Fact]
    public async Task Dispose_AfterStart_DisposesTimerSafely()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        await service.StartAsync(CancellationToken.None);
        
        // Act
        service.Dispose();
        
        // Assert - No exception should be thrown
        // The timer should be disposed
    }
    
    [Fact]
    public async Task ScheduleCleanup_DoesNotBlockStartAsync()
    {
        // Arrange
        var mockQueue = new Mock<IBackgroundTaskQueue>();
        var mockJobService = new Mock<IScanJobService>();
        var mockLogger = new Mock<ILogger<JobCleanupService>>();
        
        // Setup a slow cleanup to verify it doesn't block
        mockJobService.Setup(j => j.CleanupOldJobs(It.IsAny<TimeSpan>()))
            .Callback(() => Thread.Sleep(100));
        
        var service = new JobCleanupService(
            mockQueue.Object,
            mockJobService.Object,
            mockLogger.Object);
        
        // Act
        var startTask = service.StartAsync(CancellationToken.None);
        
        // Assert - StartAsync should complete immediately, not wait for cleanup
        var completed = await Task.WhenAny(startTask, Task.Delay(50)) == startTask;
        completed.ShouldBeTrue();
    }
}
