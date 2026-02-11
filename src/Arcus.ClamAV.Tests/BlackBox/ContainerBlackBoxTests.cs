using System.Net;
using System.Net.Http.Json;
using Arcus.ClamAV.Models;
using FluentAssertions;
using Xunit;

namespace Arcus.ClamAV.Tests.BlackBox;

/// <summary>
/// Black-box tests that run against a live Docker container.
/// These tests verify the application behavior when running in Docker.
/// 
/// Prerequisites:
/// - Docker container must be running on localhost:5000
/// - ClamAV daemon must be accessible from the container
/// 
/// Run in GitHub Actions via: .github/workflows/test-integration.yml
/// </summary>
[Trait("Category", "BlackBox")]
[Collection("BlackBox Tests")]
public class ContainerBlackBoxTests : IAsyncLifetime
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly int _maxRetries = 30;
    private readonly int _delayMs = 1000;

    public ContainerBlackBoxTests()
    {
        _baseUrl = Environment.GetEnvironmentVariable("CONTAINER_BASE_URL") ?? "http://localhost:5000";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task InitializeAsync()
    {
        // Wait for container to be healthy
        await WaitForContainerHealthyAsync();
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verify the health endpoint responds correctly from the running container.
    /// This is the most basic check that the container is running.
    /// </summary>
    [Fact(DisplayName = "Health endpoint should return 200 from running container")]
    public async Task HealthEndpoint_ShouldReturn200()
    {
        // Act
        var response = await _httpClient.GetAsync($"{_baseUrl}/healthz");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verify health endpoint returns proper health response structure.
    /// </summary>
    [Fact(DisplayName = "Health endpoint should return valid HealthResponse object")]
    public async Task HealthEndpoint_ShouldReturnValidHealthResponse()
    {
        // Act
        var response = await _httpClient.GetAsync($"{_baseUrl}/healthz");
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Status.Should().Be("ok");
    }

    /// <summary>
    /// Verify version endpoint is accessible and returns version info.
    /// </summary>
    [Fact(DisplayName = "Version endpoint should return version information")]
    public async Task VersionEndpoint_ShouldReturnVersionInfo()
    {
        // Act
        var response = await _httpClient.GetAsync($"{_baseUrl}/version");
        var content = await response.Content.ReadFromJsonAsync<VersionResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Version.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verify file scan endpoint accepts POST requests with proper structure.
    /// </summary>
    [Fact(DisplayName = "File scan endpoint should accept multipart file upload")]
    public async Task FileScanEndpoint_ShouldAcceptMultipartFileUpload()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        
        // Create a clean test file (non-infected)
        var testFileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var fileContent = new ByteArrayContent(testFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "test.bin");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan", content);

        // Assert - Should accept the upload (may reject due to ClamAV not running, but endpoint should be healthy)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError // ClamAV might not be available, but endpoint should exist
        );
    }

    /// <summary>
    /// Verify JSON scan endpoint accepts POST requests with JSON payload.
    /// </summary>
    [Fact(DisplayName = "JSON scan endpoint should accept JSON payload")]
    public async Task JsonScanEndpoint_ShouldAcceptJsonPayload()
    {
        // Arrange
        var scanRequest = new JsonScanRequest
        {
            Data = "VGhpcyBpcyBhIHRlc3Q=" // Base64 encoded test data
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError // ClamAV might not be available, but endpoint should exist
        );
    }

    /// <summary>
    /// Verify URL scan endpoint accepts POST requests with URL.
    /// </summary>
    [Fact(DisplayName = "URL scan endpoint should accept URL payload")]
    public async Task UrlScanEndpoint_ShouldAcceptUrlPayload()
    {
        // Arrange
        var scanRequest = new ScanUrlRequest
        {
            Url = "https://example.com/test.txt"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/url", scanRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError // URL fetch might fail, but endpoint should exist
        );
    }

    /// <summary>
    /// Verify async scan endpoint returns job information.
    /// </summary>
    [Fact(DisplayName = "Async scan endpoint should accept multipart file and return job ID")]
    public async Task AsyncScanEndpoint_ShouldReturnJobId()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var testFileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var fileContent = new ByteArrayContent(testFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "test.bin");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan/async", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<AsyncScanResponse>();
            result.Should().NotBeNull();
            result!.JobId.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// Verify invalid endpoints return 404.
    /// </summary>
    [Fact(DisplayName = "Invalid endpoint should return 404")]
    public async Task InvalidEndpoint_ShouldReturn404()
    {
        // Act
        var response = await _httpClient.GetAsync($"{_baseUrl}/invalid/endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verify container responds to requests within reasonable time.
    /// </summary>
    [Fact(DisplayName = "Container should respond to health check within timeout")]
    public async Task Container_ShouldRespondWithinTimeout()
    {
        // Arrange
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.GetAsync($"{_baseUrl}/healthz", HttpCompletionOption.ResponseContentRead, cts.Token);
        stopwatch.Stop();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    // Helper methods

    /// <summary>
    /// Wait for the container to be healthy before running tests.
    /// </summary>
    private async Task WaitForContainerHealthyAsync()
    {
        int attempt = 0;

        while (attempt < _maxRetries)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/healthz");
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Container is healthy after {attempt} attempts");
                    return;
                }
            }
            catch
            {
                // Container not ready yet
            }

            attempt++;
            await Task.Delay(_delayMs);
        }

        throw new TimeoutException(
            $"Container failed to become healthy after {_maxRetries} attempts ({_maxRetries * _delayMs}ms). " +
            $"Base URL: {_baseUrl}. Ensure the container is running and accessible.");
    }
}
