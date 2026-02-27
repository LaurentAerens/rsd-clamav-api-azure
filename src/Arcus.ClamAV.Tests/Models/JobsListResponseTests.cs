using Arcus.ClamAV.Models;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.Models;

public class JobsListResponseTests
{
    [Fact]
    public void JobsListResponse_ShouldInitializeWithDefaultValues()
    {
        // Act
        var response = new JobsListResponse();

        // Assert
        response.Jobs.ShouldNotBeNull();
        response.Jobs.ShouldBeEmpty();
        response.Count.ShouldBe(0);
    }

    [Fact]
    public void JobsListResponse_ShouldAllowSettingProperties()
    {
        // Arrange
        var jobs = new List<JobSummary>
        {
            new JobSummary { JobId = "job1", Status = "completed" },
            new JobSummary { JobId = "job2", Status = "scanning" }
        };

        // Act
        var response = new JobsListResponse
        {
            Jobs = jobs,
            Count = 2
        };

        // Assert
        response.Jobs.ShouldBe(jobs);
        response.Count.ShouldBe(2);
    }

    [Fact]
    public void JobSummary_ShouldInitializeWithDefaultValues()
    {
        // Act
        var summary = new JobSummary();

        // Assert
        summary.JobId.ShouldBe(string.Empty);
        summary.Status.ShouldBe(string.Empty);
        summary.FileName.ShouldBeNull();
        summary.FileSize.ShouldBeNull();
        summary.CreatedAt.ShouldBe(default);
        summary.CompletedAt.ShouldBeNull();
        summary.ScanDurationMs.ShouldBeNull();
    }

    [Fact]
    public void JobSummary_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var createdAt = DateTime.UtcNow;
        var completedAt = DateTime.UtcNow.AddSeconds(5);

        // Act
        var summary = new JobSummary
        {
            JobId = "test-job-123",
            Status = "completed",
            FileName = "document.pdf",
            FileSize = 1024000,
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            ScanDurationMs = 500.5
        };

        // Assert
        summary.JobId.ShouldBe("test-job-123");
        summary.Status.ShouldBe("completed");
        summary.FileName.ShouldBe("document.pdf");
        summary.FileSize.ShouldBe(1024000);
        summary.CreatedAt.ShouldBe(createdAt);
        summary.CompletedAt.ShouldBe(completedAt);
        summary.ScanDurationMs.ShouldBe(500.5);
    }

    [Fact]
    public void JobSummary_ShouldAllowNullableProperties()
    {
        // Act
        var summary = new JobSummary
        {
            JobId = "job-456",
            Status = "scanning",
            FileName = null,
            FileSize = null,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
            ScanDurationMs = null
        };

        // Assert
        summary.JobId.ShouldBe("job-456");
        summary.Status.ShouldBe("scanning");
        summary.FileName.ShouldBeNull();
        summary.FileSize.ShouldBeNull();
        summary.CompletedAt.ShouldBeNull();
        summary.ScanDurationMs.ShouldBeNull();
    }

    [Fact]
    public void JobSummary_ShouldHandleLargeFileSizes()
    {
        // Arrange
        long largeFileSize = 5L * 1024 * 1024 * 1024; // 5GB

        // Act
        var summary = new JobSummary
        {
            FileSize = largeFileSize
        };

        // Assert
        summary.FileSize.ShouldBe(largeFileSize);
    }

    [Fact]
    public void JobSummary_ShouldHandleSpecialCharactersInFileName()
    {
        // Act
        var summary = new JobSummary
        {
            FileName = "test file (1) [copy].pdf"
        };

        // Assert
        summary.FileName.ShouldBe("test file (1) [copy].pdf");
    }

    [Fact]
    public void JobSummary_ShouldHandleVerySmallScanDuration()
    {
        // Act
        var summary = new JobSummary
        {
            ScanDurationMs = 0.001
        };

        // Assert
        summary.ScanDurationMs.ShouldBe(0.001);
    }

    [Fact]
    public void JobSummary_ShouldHandleZeroFileSize()
    {
        // Act
        var summary = new JobSummary
        {
            FileSize = 0
        };

        // Assert
        summary.FileSize.ShouldBe(0);
    }

    [Fact]
    public void JobsListResponse_ShouldHandleEmptyJobsList()
    {
        // Act
        var response = new JobsListResponse
        {
            Jobs = Enumerable.Empty<JobSummary>(),
            Count = 0
        };

        // Assert
        response.Jobs.ShouldBeEmpty();
        response.Count.ShouldBe(0);
    }

    [Fact]
    public void JobsListResponse_ShouldHandleMultipleJobs()
    {
        // Arrange
        var jobs = new List<JobSummary>();
        for (int i = 0; i < 10; i++)
        {
            jobs.Add(new JobSummary
            {
                JobId = $"job-{i}",
                Status = i % 2 == 0 ? "completed" : "scanning",
                FileName = $"file{i}.bin",
                FileSize = 1024 * i,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        // Act
        var response = new JobsListResponse
        {
            Jobs = jobs,
            Count = jobs.Count
        };

        // Assert
        response.Jobs.Count().ShouldBe(10);
        response.Count.ShouldBe(10);
    }
}
