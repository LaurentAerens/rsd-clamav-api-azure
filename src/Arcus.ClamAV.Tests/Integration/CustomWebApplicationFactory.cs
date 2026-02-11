using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Arcus.ClamAV.Services;

namespace Arcus.ClamAV.Tests.Integration;

/// <summary>
/// Custom web application factory for testing that sets up an in-memory test server.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Mock<IClamAvInfoService> _clamAvInfoServiceMock;
    private readonly Mock<IScanProcessingService> _scanProcessingServiceMock;

    public CustomWebApplicationFactory()
    {
        _clamAvInfoServiceMock = new Mock<IClamAvInfoService>();
        _scanProcessingServiceMock = new Mock<IScanProcessingService>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real ClamAvInfoService
            var clamAvInfoDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IClamAvInfoService));
            if (clamAvInfoDescriptor != null)
                services.Remove(clamAvInfoDescriptor);

            // Remove the real ScanProcessingService
            var scanProcessingDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IScanProcessingService));
            if (scanProcessingDescriptor != null)
                services.Remove(scanProcessingDescriptor);

            // Add mocks
            services.AddSingleton(_clamAvInfoServiceMock.Object);
            services.AddScoped(_ => _scanProcessingServiceMock.Object);
        });
    }

    public Mock<IClamAvInfoService> ClamAvInfoServiceMock => _clamAvInfoServiceMock;
    public Mock<IScanProcessingService> ScanProcessingServiceMock => _scanProcessingServiceMock;
}

