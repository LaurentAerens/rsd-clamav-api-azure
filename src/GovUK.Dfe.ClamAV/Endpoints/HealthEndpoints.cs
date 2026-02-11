using GovUK.Dfe.ClamAV.Models;
using GovUK.Dfe.ClamAV.Services;

namespace GovUK.Dfe.ClamAV.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Health check endpoint
        app.MapGet("/healthz", () => Results.Ok(new HealthResponse { Status = "ok" }))
            .WithTags("Health")
            .Produces<HealthResponse>(200)
            .WithName("HealthCheck")
            .WithDescription("Basic health check endpoint");

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

