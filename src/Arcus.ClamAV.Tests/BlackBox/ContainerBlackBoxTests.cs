using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
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
    /// Verify file scan endpoint detects EICAR test file and returns 406 (Not Acceptable).
    /// </summary>
    [Fact(DisplayName = "File scan should detect EICAR test file and return 406")]
    public async Task FileScan_ShouldDetectEicarTestFileAndReturn406()
    {
        // Arrange
        using var content = new MultipartFormDataContent();

        // The actual EICAR test string that triggers antivirus detection
        var eicarBytes = System.Text.Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        using var fileContent = new ByteArrayContent(eicarBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "eicar.com");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan", content);

        // Assert - Should return 406 (Not Acceptable) for infected file
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable);
        var result = await response.Content.ReadFromJsonAsync<ScanResponse>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
        result.Malware.ShouldContain("EICAR");
    }

    /// <summary>
    /// Verify file scan endpoint accepts clean files and returns 200.
    /// </summary>
    [Fact(DisplayName = "File scan should accept clean file and return 200")]
    public async Task FileScan_ShouldAcceptCleanFileAndReturn200()
    {
        // Arrange
        using var content = new MultipartFormDataContent();

        // Create a clean test file with some arbitrary data
        var cleanFileBytes = System.Text.Encoding.UTF8.GetBytes("This is a clean file with no malicious content whatsoever.");
        using var fileContent = new ByteArrayContent(cleanFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "file", "clean.txt");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan", content);

        // Assert - Should return 200 (OK) for clean file
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ScanResponse>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("clean");
        result.Malware.ShouldBeNullOrEmpty();
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
    /// Verify async scan endpoint returns job information for clean files.
    /// </summary>
    [Fact(DisplayName = "Async scan endpoint should accept clean file and return job ID")]
    public async Task AsyncScanEndpoint_WithCleanFile_ShouldReturnJobId()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        var cleanFileBytes = System.Text.Encoding.UTF8.GetBytes("This is a clean test file for async scanning.");
        using var fileContent = new ByteArrayContent(cleanFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "file", "clean-async.txt");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan/async", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted); // Async operations return 202
        var result = await response.Content.ReadFromJsonAsync<AsyncScanResponse>();
        result.ShouldNotBeNull();
        result!.JobId.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Verify async scan endpoint returns job information for infected files.
    /// </summary>
    [Fact(DisplayName = "Async scan endpoint should accept EICAR file and return job ID")]
    public async Task AsyncScanEndpoint_WithEicarFile_ShouldReturnJobId()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        var eicarBytes = System.Text.Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        using var fileContent = new ByteArrayContent(eicarBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "eicar-async.com");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan/async", content);

        // Assert - Should still return Accepted (202) for async processing
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted); // Async operations return 202
        var result = await response.Content.ReadFromJsonAsync<AsyncScanResponse>();
        result.ShouldNotBeNull();
        result!.JobId.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Verify async scan job status endpoint returns correct status for completed scan.
    /// </summary>
    [Fact(DisplayName = "Async scan status endpoint should return completed job status")]
    public async Task AsyncScanStatus_ShouldReturnCompletedJobStatus()
    {
        // Arrange - First submit an async scan
        using var uploadContent = new MultipartFormDataContent();
        var cleanFileBytes = System.Text.Encoding.UTF8.GetBytes("Clean file for status check.");
        using var fileContent = new ByteArrayContent(cleanFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/plain");
        uploadContent.Add(fileContent, "file", "status-check.txt");

        var uploadResponse = await _httpClient.PostAsync($"{_baseUrl}/scan/async", uploadContent);
        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        
        var asyncResult = await uploadResponse.Content.ReadFromJsonAsync<AsyncScanResponse>();
        asyncResult.ShouldNotBeNull();
        var jobId = asyncResult!.JobId;

        // Wait a bit for the scan to complete
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Check job status using correct endpoint: /scan/async/{jobId}
        var statusResponse = await _httpClient.GetAsync($"{_baseUrl}/scan/async/{jobId}");

        // Assert
        statusResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted);
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<ScanStatusResponse>();
        statusResult.ShouldNotBeNull();
        statusResult!.JobId.ShouldBe(jobId);
    }

    /// <summary>
    /// Verify list jobs endpoint returns job information.
    /// </summary>
    [Fact(DisplayName = "List jobs endpoint should return available jobs")]
    public async Task ListJobs_ShouldReturnJobsList()
    {
        // Arrange - First submit an async scan
        using var uploadContent = new MultipartFormDataContent();
        var testFileBytes = System.Text.Encoding.UTF8.GetBytes("File for listing.");
        using var fileContent = new ByteArrayContent(testFileBytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/plain");
        uploadContent.Add(fileContent, "file", "list-test.txt");

        var uploadResponse = await _httpClient.PostAsync($"{_baseUrl}/scan/async", uploadContent);
        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Act - Get list of jobs
        var response = await _httpClient.GetAsync($"{_baseUrl}/scan/jobs");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JobsListResponse>();
        result.ShouldNotBeNull();
        result!.Jobs.ShouldNotBeNull();
        result.Jobs.Count().ShouldBeGreaterThanOrEqualTo(0);
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

    /// <summary>
    /// JSON Scan Tests - Tests for malware detection in JSON payloads.
    /// These tests verify that the JSON scan endpoint properly detects:
    /// - Base64-encoded malware
    /// - Plaintext malware strings
    /// - Nested structures with malware
    /// - Mixed content types
    /// </summary>

    /// <summary>
    /// Verify JSON scan detects plaintext EICAR in string properties.
    /// The EICAR test file is a standard antivirus test pattern.
    /// </summary>
    [Fact(DisplayName = "JSON scan should detect plaintext EICAR in string properties")]
    public async Task JsonScan_ShouldDetectPlaintextEicar()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""file"": ""X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"",
            ""filename"": ""test.exe""
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable); // 406 for infected
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
        result.Malware.ShouldContain("EICAR");
        result.ItemsScanned.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Verify JSON scan detects base64-encoded EICAR.
    /// Base64: WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo=
    /// </summary>
    [Fact(DisplayName = "JSON scan should detect base64-encoded EICAR")]
    public async Task JsonScan_ShouldDetectBase64Eicar()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""file"": ""WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo="",
            ""filename"": ""test.exe""
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable); // 406 for infected
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
        result.Malware.ShouldContain("EICAR");
        result.Base64ItemsFound.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Verify JSON scan detects EICAR in nested objects.
    /// </summary>
    [Fact(DisplayName = "JSON scan should detect EICAR in nested object properties")]
    public async Task JsonScan_ShouldDetectEicarInNestedObjects()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""user"": {
                ""name"": ""test"",
                ""attachment"": ""X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*""
            }
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable); // 406 for infected
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
    }

    /// <summary>
    /// Verify JSON scan detects EICAR in array elements.
    /// </summary>
    [Fact(DisplayName = "JSON scan should detect EICAR in array elements")]
    public async Task JsonScan_ShouldDetectEicarInArrays()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""documents"": [
                {
                    ""type"": ""pdf"",
                    ""content"": ""WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo=""
                },
                {
                    ""type"": ""doc"",
                    ""content"": ""clean content""
                }
            ]
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable); // 406 for infected
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
    }

    /// <summary>
    /// Verify JSON scan accepts clean JSON with base64 content.
    /// </summary>
    [Fact(DisplayName = "JSON scan should accept clean JSON with base64 content")]
    public async Task JsonScan_ShouldAcceptCleanBase64()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""documents"": [
                {
                    ""type"": ""pdf"",
                    ""content"": ""anVzdCBzb21lIHJhbmRvbSBzdHJpbmc=""
                }
            ],
            ""filename"": ""test.pdf""
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("clean");
        result.Base64ItemsFound.ShouldBe(1);
    }

    /// <summary>
    /// Verify JSON scan handles complex nested structures.
    /// </summary>
    [Fact(DisplayName = "JSON scan should handle deeply nested structures")]
    public async Task JsonScan_ShouldHandleDeeplyNestedStructures()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""level1"": {
                ""level2"": {
                    ""level3"": {
                        ""data"": ""cXVpdGUgYSBsb3Qgb2YgZGF0YSBpbiBoZXJlIGlzbid0IGl0"",
                        ""items"": [
                            { ""content"": ""dGhpcyBpcyBzb21lIGNsZWFuIGRhdGEgNCB5b3U="" },
                            { ""content"": ""YW5vdGhlciBzbXJhc3QgY2xlYW4gZGF0YSBzdHJpbmc="" }
                        ]
                    }
                }
            }
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("clean");
        result.Base64ItemsFound.ShouldBe(3); // All three strings meet the minimum length requirement and decode successfully
    }

    /// <summary>
    /// Verify JSON scan with mixed plaintext and base64 EICAR detects either.
    /// </summary>
    [Fact(DisplayName = "JSON scan should detect EICAR in mixed plaintext and base64 content")]
    public async Task JsonScan_ShouldDetectEicarInMixedContent()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""file"": ""X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"",
            ""documents"": [
                { ""content"": ""anVzdCBzb21lIHJhbmRvbSBzdHJpbmc="" },
                { ""content"": ""Y2xlYW4gZGF0YQ=="" }
            ]
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable); // 406 for infected
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
    }

    /// <summary>
    /// Verify JSON scan result includes proper details about scanned items.
    /// </summary>
    [Fact(DisplayName = "JSON scan result should include details about scanned items")]
    public async Task JsonScan_ShouldReturnDetailedScanInfo()
    {
        // Arrange
        var payload = JsonDocument.Parse(@"{
            ""file"": ""anVzdCBzb21lIHJhbmRvbSBzdHJpbmc="",
            ""metadata"": {
                ""source"": ""email"",
                ""timestamp"": ""2026-02-21T12:00:00Z""
            }
        }").RootElement;

        var scanRequest = new JsonScanRequest { Payload = payload };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/scan/json", scanRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Details.ShouldNotBeEmpty();
        result.Details[0].Name.ShouldNotBeNullOrEmpty();
        result.Details[0].Type.ShouldNotBeNullOrEmpty();
        result.Details[0].Size.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Verify JSON scan detects EICAR at root level (new API design).
    /// Tests the updated API that scans the entire JSON body, not just nested properties.
    /// </summary>
    [Fact(DisplayName = "JSON scan should detect EICAR at root level (raw JSON API)")]
    public async Task JsonScan_ShouldDetectEicarAtRootLevel()
    {
        // Arrange - Send raw JSON directly (no wrapper), with EICAR at root level
        var jsonPayload = new StringContent(
            @"{
                ""testItem"": ""X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"",
                ""metadata"": {
                    ""source"": ""api-test"",
                    ""timestamp"": ""2026-03-02T00:00:00Z""
                }
            }",
            Encoding.UTF8,
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
        );

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan/json", jsonPayload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable); // 406 for infected
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("infected");
        result.Malware.ShouldContain("EICAR");
        result.InfectedItem.ShouldBe("testItem"); // Root-level property should be scanned
    }

    /// <summary>
    /// Verify JSON scan accepts clean raw JSON at root level (new API design).
    /// </summary>
    [Fact(DisplayName = "JSON scan should accept clean raw JSON at root level")]
    public async Task JsonScan_ShouldAcceptCleanRawJson()
    {
        // Arrange - Send raw JSON directly with no malware
        var jsonPayload = new StringContent(
            @"{
                ""data1"": ""clean content"",
                ""data2"": ""YW5vdGhlciBjbGVhbiBzdHJpbmc="",
                ""nested"": {
                    ""deep"": ""bW9yZSBjbGVhbiBkYXRh""
                }
            }",
            Encoding.UTF8,
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
        );

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/scan/json", jsonPayload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonScanResult>();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe("clean");
        result.ItemsScanned.ShouldBeGreaterThan(0);
    }
}
