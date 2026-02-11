using System.Net;
using System.Net.Http.Json;
using GovUK.Dfe.ClamAV.Models;
using GovUK.Dfe.ClamAV.Services;

namespace GovUK.Dfe.ClamAV.Tests.Integration;

public class ScanEndpointTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact(Skip = "Requires running ClamAV instance for synchronous scanning")]
    public async Task SyncScanEndpoint_WithValidFile_ShouldReturn200()
    {
        // Arrange
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("test content for scanning");
        using var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(fileBytes), "file", "test.txt");

        // Act
        var response = await _client.PostAsync("/scan", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Skip = "Requires running ClamAV instance for synchronous scanning")]
    public async Task SyncScanEndpoint_WithEmptyFile_ShouldReturn400()
    {
        // Arrange
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("");
        using var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(fileBytes), "file", "empty.txt");

        // Act
        var response = await _client.PostAsync("/scan", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SyncScanEndpoint_WithoutFile_ShouldReturn400()
    {
        // Arrange
        using var formData = new MultipartFormDataContent();

        // Act
        var response = await _client.PostAsync("/scan", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AsyncScanEndpoint_WithValidFile_ShouldReturn202()
    {
        // Arrange
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("test content for async scan");
        using var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(fileBytes), "file", "test.txt");

        // Arrange: Setup mock
        _factory.ScanProcessingServiceMock
            .Setup(s => s.ProcessFileScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await _client.PostAsync("/scan/async", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task AsyncScanEndpoint_ShouldReturnAsyncScanResponse()
    {
        // Arrange
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("test content for async scan response");
        using var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(fileBytes), "file", "test.txt");

        _factory.ScanProcessingServiceMock
            .Setup(s => s.ProcessFileScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await _client.PostAsync("/scan/async", formData);
        var content = await response.Content.ReadFromJsonAsync<AsyncScanResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        content.Should().NotBeNull();
        content!.JobId.Should().NotBeNullOrEmpty();
        content.Status.Should().Be("queued");
        content.StatusUrl.Should().Contain(content.JobId);
    }

    [Fact]
    public async Task AsyncUrlScanEndpoint_WithValidUrl_ShouldReturn202()
    {
        // Arrange
        var scanRequest = new ScanUrlRequest { Url = "https://example.com/file.zip", IsBase64 = false };

        _factory.ScanProcessingServiceMock
            .Setup(s => s.ProcessUrlScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await _client.PostAsJsonAsync("/scan/async/url", scanRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task AsyncUrlScanEndpoint_WithInvalidUrl_ShouldReturn400()
    {
        // Arrange
        var scanRequest = new ScanUrlRequest { Url = "not-a-valid-url", IsBase64 = false };

        // Act
        var response = await _client.PostAsJsonAsync("/scan/async/url", scanRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AsyncUrlScanEndpoint_WithEmptyUrl_ShouldReturn400()
    {
        // Arrange
        var scanRequest = new ScanUrlRequest { Url = "", IsBase64 = false };

        // Act
        var response = await _client.PostAsJsonAsync("/scan/async/url", scanRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetScanStatusEndpoint_WithValidJobId_ShouldReturn200()
    {
        // Arrange
        // First, create a scan job
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("test content for status check");
        using var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(fileBytes), "file", "test.txt");

        _factory.ScanProcessingServiceMock
            .Setup(s => s.ProcessFileScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var asyncResponse = await _client.PostAsync("/scan/async", formData);
        var scanJobContent = await asyncResponse.Content.ReadFromJsonAsync<AsyncScanResponse>();

        // Act
        var response = await _client.GetAsync($"/scan/async/{scanJobContent!.JobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetScanStatusEndpoint_WithInvalidJobId_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/scan/async/invalid-job-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListJobsEndpoint_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/scan/jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListJobsEndpoint_ShouldReturnJobsListResponse()
    {
        // Act
        var response = await _client.GetAsync("/scan/jobs");
        var content = await response.Content.ReadFromJsonAsync<JobsListResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Jobs.Should().NotBeNull();
        content.Count.Should().BeGreaterThanOrEqualTo(0);
    }
}
