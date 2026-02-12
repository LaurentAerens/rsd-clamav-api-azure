using System.Net;
using System.Net.Http.Json;
using Arcus.ClamAV.Models;

namespace Arcus.ClamAV.Tests.Integration;

public class HealthEndpointTests : IAsyncLifetime
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

    [Fact]
    public async Task HealthzEndpoint_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthzEndpoint_ShouldReturnHealthResponse()
    {
        // Act
        var response = await _client.GetAsync("/healthz");
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldNotBeNull();
        content!.Status.ShouldBe("ok");
    }

    [Fact]
    public async Task VersionEndpoint_ShouldReturn200()
    {
        // Arrange
        _factory.ClamAvInfoServiceMock
            .Setup(s => s.GetVersionAsync())
            .ReturnsAsync("ClamAV 0.103.0/26621/Fri Mar  3 12:48:46 2023");

        // Act
        var response = await _client.GetAsync("/version");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VersionEndpoint_ShouldReturnVersionResponse()
    {
        // Arrange
        var expectedVersion = "ClamAV 0.103.0/26621/Fri Mar  3 12:48:46 2023";
        _factory.ClamAvInfoServiceMock
            .Setup(s => s.GetVersionAsync())
            .ReturnsAsync(expectedVersion);

        // Act
        var response = await _client.GetAsync("/version");
        var content = await response.Content.ReadFromJsonAsync<VersionResponse>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldNotBeNull();
        content!.ClamAvVersion.ShouldBe(expectedVersion);
    }
}

