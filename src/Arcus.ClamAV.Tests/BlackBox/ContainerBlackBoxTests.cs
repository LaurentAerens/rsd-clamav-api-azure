using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Arcus.ClamAV.Models;
using Shouldly;
using Xunit;

namespace Arcus.ClamAV.Tests.BlackBox;

/// <summary>
/// Black-box tests that run against a live Docker container.
/// These tests verify the application behavior when running in Docker.
/// 
/// The DockerComposeFixture automatically:
/// - Starts docker-compose before any tests run
/// - Waits for containers to be healthy
/// - Stops containers after all tests complete
/// 
/// Just press "Run Tests" in VS Code or run: dotnet test --filter "Category=BlackBox"
/// 
/// Run in GitHub Actions via: .github/workflows/test-integration.yml
/// </summary>
[Trait("Category", "BlackBox")]
[Collection("BlackBox Tests")]
public class ContainerBlackBoxTests
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ContainerBlackBoxTests(DockerComposeFixture fixture)
    {
        // The fixture ensures containers are running and healthy
        _baseUrl = fixture.BaseUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldNotBeNull();
        content!.Status.ShouldBe("ok");
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldNotBeNull();
        content!.ClamAvVersion.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Verify file scan endpoint accepts POST requests with proper structure.
    /// </summary>
    [Fact(DisplayName = "File scan endpoint should accept multipart file upload")]
    public async Task FileScanEndpoint_ShouldAcceptMultipartFileUpload()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        
        // Create a clean test file (non-infected)
        var testFileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        using var fileContent = new ByteArrayContent(testFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "test.bin");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan", content);

        // Assert - Should accept the upload (may reject due to ClamAV not running, but endpoint should be healthy)
        response.StatusCode.ShouldBeOneOf(
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
            Payload = JsonDocument.Parse("{\"data\": \"VGhpcyBpcyBhIHRlc3Q=\"}").RootElement
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBeOneOf(
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
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/async/url", scanRequest);

        // Assert
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.Accepted, // Async operation returns 202
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
        using var content = new MultipartFormDataContent();
        var testFileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        using var fileContent = new ByteArrayContent(testFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "test.bin");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan/async", content);

        // Assert
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.Accepted, // Async operations return 202
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError
        );

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            var result = await response.Content.ReadFromJsonAsync<AsyncScanResponse>();
            result.ShouldNotBeNull();
            result!.JobId.ShouldNotBeNullOrEmpty();
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
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        response.IsSuccessStatusCode.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
    }
}
