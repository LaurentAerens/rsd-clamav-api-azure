using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;

namespace Arcus.ClamAV.Tests.Services;

public class ScanJobServiceTests
{
    private readonly ScanJobService _jobService;
    private readonly Mock<ILogger<ScanJobService>> _loggerMock;

    public ScanJobServiceTests()
    {
        _loggerMock = new Mock<ILogger<ScanJobService>>();
        _jobService = new ScanJobService(_loggerMock.Object);
    }

    [Fact]
    public void CreateJob_WithValidInput_ShouldReturnJobId()
    {
        // Act
        var jobId = _jobService.CreateJob("test.exe", 1024);

        // Assert
        jobId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(jobId, out _).ShouldBeTrue();
    }

    [Fact]
    public void CreateJob_ShouldCreateJobWithCorrectProperties()
    {
        // Arrange
        var fileName = "malware.zip";
        var fileSize = 5120L;

        // Act
        var jobId = _jobService.CreateJob(fileName, fileSize);
        var job = _jobService.GetJob(jobId);

        // Assert
        job.ShouldNotBeNull();
        job!.JobId.ShouldBe(jobId);
        job.FileName.ShouldBe(fileName);
        job.FileSize.ShouldBe(fileSize);
        job.Status.ShouldBe("queued");
        (DateTime.UtcNow - job.CreatedAt).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetJob_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var job = _jobService.GetJob("nonexistent-id");

        // Assert
        job.ShouldBeNull();
    }

    [Fact]
    public void UpdateJobStatus_ShouldUpdateStatus()
    {
        // Arrange
        var jobId = _jobService.CreateJob("test.exe", 1024);

        // Act
        _jobService.UpdateJobStatus(jobId, "scanning");
        var job = _jobService.GetJob(jobId);

        // Assert
        job!.Status.ShouldBe("scanning");
    }

    [Fact]
    public void UpdateJobStatus_WithMalwareName_ShouldUpdateMalware()
    {
        // Arrange
        var jobId = _jobService.CreateJob("test.exe", 1024);

        // Act
        _jobService.UpdateJobStatus(jobId, "infected", malware: "Win.Trojan.Generic");
        var job = _jobService.GetJob(jobId);

        // Assert
        job!.Malware.ShouldBe("Win.Trojan.Generic");
    }

    [Fact]
    public void UpdateJobStatus_WithError_ShouldUpdateError()
    {
        // Arrange
        var jobId = _jobService.CreateJob("test.exe", 1024);

        // Act
        _jobService.UpdateJobStatus(jobId, "error", error: "Connection timeout");
        var job = _jobService.GetJob(jobId);

        // Assert
        job!.Error.ShouldBe("Connection timeout");
    }

    [Fact]
    public void CompleteJob_ShouldSetCompletedAt()
    {
        // Arrange
        var jobId = _jobService.CreateJob("test.exe", 1024);
        var beforeComplete = DateTime.UtcNow;

        // Act
        _jobService.CompleteJob(jobId);
        var job = _jobService.GetJob(jobId);

        // Assert
        job!.CompletedAt.ShouldNotBeNull();
        job.CompletedAt!.Value.ShouldBeGreaterThanOrEqualTo(beforeComplete);
    }

    [Fact]
    public void CleanupOldJobs_ShouldRemoveJobsOlderThanMaxAge()
    {
        // Arrange
        // Create multiple jobs, but we can't easily change their CreatedAt time
        // So we'll just verify the method runs without error
        var jobId1 = _jobService.CreateJob("test1.exe", 1024);
        var jobId2 = _jobService.CreateJob("test2.exe", 2048);

        // Act
        _jobService.CleanupOldJobs(TimeSpan.FromSeconds(0)); // Everything should be cleaned

        // Assert
        var allJobs = _jobService.GetAllJobs();
        allJobs.ShouldBeEmpty();
    }

    [Fact]
    public void GetAllJobs_ShouldReturnAllJobsOrderedByCreatedAtDescending()
    {
        // Arrange
        var jobId1 = _jobService.CreateJob("test1.exe", 1024);
        var jobId2 = _jobService.CreateJob("test2.exe", 2048);
        var jobId3 = _jobService.CreateJob("test3.exe", 4096);

        // Act
        var allJobs = _jobService.GetAllJobs().ToList();

        // Assert
        allJobs.Count.ShouldBe(3);
        allJobs[0].JobId.ShouldBe(jobId3); // Most recent first
        allJobs[1].JobId.ShouldBe(jobId2);
        allJobs[2].JobId.ShouldBe(jobId1); // Oldest last
    }

    [Fact]
    public void CreateJob_MultipleCallsWithSameInput_ShouldGenerateUniqueIds()
    {
        // Act
        var jobId1 = _jobService.CreateJob("test.exe", 1024);
        var jobId2 = _jobService.CreateJob("test.exe", 1024);

        // Assert
        jobId1.ShouldNotBe(jobId2);
    }
}

