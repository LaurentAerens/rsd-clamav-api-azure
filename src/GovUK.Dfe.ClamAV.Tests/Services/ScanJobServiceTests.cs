using GovUK.Dfe.ClamAV.Models;
using GovUK.Dfe.ClamAV.Services;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.ClamAV.Tests.Services;

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
        jobId.Should().NotBeNullOrEmpty();
        Guid.TryParse(jobId, out _).Should().BeTrue();
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
        job.Should().NotBeNull();
        job!.JobId.Should().Be(jobId);
        job.FileName.Should().Be(fileName);
        job.FileSize.Should().Be(fileSize);
        job.Status.Should().Be("queued");
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetJob_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var job = _jobService.GetJob("nonexistent-id");

        // Assert
        job.Should().BeNull();
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
        job!.Status.Should().Be("scanning");
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
        job!.Malware.Should().Be("Win.Trojan.Generic");
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
        job!.Error.Should().Be("Connection timeout");
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
        job!.CompletedAt.Should().NotBeNull();
        job.CompletedAt!.Value.Should().BeOnOrAfter(beforeComplete);
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
        allJobs.Should().BeEmpty();
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
        allJobs.Should().HaveCount(3);
        allJobs[0].JobId.Should().Be(jobId3); // Most recent first
        allJobs[1].JobId.Should().Be(jobId2);
        allJobs[2].JobId.Should().Be(jobId1); // Oldest last
    }

    [Fact]
    public void CreateJob_MultipleCallsWithSameInput_ShouldGenerateUniqueIds()
    {
        // Act
        var jobId1 = _jobService.CreateJob("test.exe", 1024);
        var jobId2 = _jobService.CreateJob("test.exe", 1024);

        // Assert
        jobId1.Should().NotBe(jobId2);
    }
}
