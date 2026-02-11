using System.Net;
using System.Net.Http.Json;
using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;

namespace Arcus.ClamAV.Tests.Integration;

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

    [Fact(Skip = "Requires running ClamAV instance for JSON scanning")]
    public async Task JsonScanEndpoint_WithCleanPayload_ShouldReturn200()
    {
        // Arrange - JSON with base64 content
        var originalContent = "Hello, this is a clean test file"u8.ToArray();
        var base64Content = Convert.ToBase64String(originalContent);
        
        var jsonPayload = new
        {
            id = "12345",
            timestamp = "2024-01-01T00:00:00Z",
            content = base64Content,
            metadata = new { type = "document", size = originalContent.Length }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/scan/json", new { payload = jsonPayload });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("clean");
        result.Base64ItemsFound.Should().Be(1);
        result.ItemsScanned.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "Requires running ClamAV instance for JSON scanning")]
    public async Task JsonScanEndpoint_WithMultipleBase64Items_ShouldScanAll()
    {
        // Arrange - JSON with multiple base64 items
        var content1 = Convert.ToBase64String("First file content"u8.ToArray());
        var content2 = Convert.ToBase64String("Second file content"u8.ToArray());
        
        var jsonPayload = new
        {
            attachments = new[]
            {
                new { name = "file1.txt", data = content1 },
                new { name = "file2.txt", data = content2 }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/scan/json", new { payload = jsonPayload });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.Should().NotBeNull();
        result!.Base64ItemsFound.Should().Be(2);
        result.Details.Should().HaveCountGreaterThan(0);
    }

    [Fact(Skip = "Requires running ClamAV instance for JSON scanning")]
    public async Task JsonScanEndpoint_WithNestedBase64_ShouldDetect()
    {
        // Arrange - JSON with deeply nested base64 content
        var base64Content = Convert.ToBase64String("Nested test content"u8.ToArray());
        
        var jsonPayload = new
        {
            envelope = new
            {
                header = new { messageId = "abc123" },
                body = new
                {
                    message = "Some text",
                    attachment = new
                    {
                        fileName = "document.pdf",
                        contentBytes = base64Content
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/scan/json", new { payload = jsonPayload });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.Should().NotBeNull();
        result!.Base64ItemsFound.Should().BeGreaterThan(0);
        result.Details.Should().Contain(d => d.Type == "base64_decoded");
    }

    [Fact(Skip = "Requires running ClamAV instance for JSON scanning")]
    public async Task JsonScanEndpoint_WithoutBase64_ShouldScanJsonText()
    {
        // Arrange - JSON without base64 content
        var jsonPayload = new
        {
            id = "test123",
            message = "Just a regular message without any base64",
            count = 42
        };

        // Act
        var response = await _client.PostAsJsonAsync("/scan/json", new { payload = jsonPayload });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.Should().NotBeNull();
        result!.Base64ItemsFound.Should().Be(0);
        result.ItemsScanned.Should().Be(1); // Just the JSON text itself
        result.Details.Should().Contain(d => d.Name == "json_payload" && d.Type == "json_text");
    }
}

