using System.IO;
using System.Net.Sockets;
using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;

namespace Arcus.ClamAV.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Health check endpoint - verifies API is running and clamd is responsive
        app.MapGet("/healthz", async (IClamAvInfoService clam) =>
        {
            try
            {
                // This will throw if clamd is not responding
                await clam.GetVersionAsync();
                return Results.Ok(new HealthResponse { Status = "ok" });
            }
            catch (SocketException)
            {
                // ClamAV daemon not ready yet
                return Results.StatusCode(503); // Service Unavailable
            }
            catch (IOException)
            {
                // ClamAV daemon not ready yet
                return Results.StatusCode(503); // Service Unavailable
            }
        })
            .WithTags("Health")
            .Produces<HealthResponse>(200)
            .Produces(503)
            .WithName("HealthCheck")
            .WithDescription("Health check - verifies API and ClamAV are ready");

        app.MapGet("/version", async (IClamAvInfoService clam) =>
        {
            var version = await clam.GetVersionAsync();
            return Results.Ok(new VersionResponse { ClamAvVersion = version });
        })
        .WithTags("Health")
        .Produces<VersionResponse>(200)
        .WithName("GetVersion")
        .WithDescription("Get ClamAV version information");
    }
}
