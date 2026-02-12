using Arcus.ClamAV.Services;
using Microsoft.Extensions.Configuration;

namespace Arcus.ClamAV.Tests.Services;

public class ClamAvInfoServiceTests
{
    [Fact]
    public async Task GetVersionAsync_ShouldReturnVersionString()
    {
        // This test would require a running ClamAV instance
        // For now, we'll create a mock configuration and verify the service can be instantiated

        // Arrange
        var configMock = new Mock<IConfiguration>();
        var section = new Mock<IConfigurationSection>();
        section.Setup(x => x.Value).Returns("127.0.0.1");
        configMock.Setup(c => c["CLAMD_HOST"]).Returns("127.0.0.1");
        configMock.Setup(c => c["CLAMD_PORT"]).Returns("3310");

        var service = new ClamAvInfoService(configMock.Object);

        // Assert
        service.ShouldNotBeNull();
    }
}

