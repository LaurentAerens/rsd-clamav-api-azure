using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;

namespace Arcus.ClamAV.Endpoints;

public static class ScanEndpoints
{
    public static void MapScanEndpoints(this IEndpointRouteBuilder app)
    {
        var scanGroup = app.MapGroup("/scan")
            .WithTags("Scan");

        // Synchronous scan
        scanGroup.MapPost("", async (IFormFile file, FileScanHandler handler) =>
            await handler.HandleSyncAsync(file))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ScanResponse>(200)
            .Produces<ScanResponse>(406)
            .Produces<ErrorResponse>(400)
            .Produces(500)
            .WithName("ScanSync")
            .WithDescription("Upload a file for synchronous virus scanning. Waits for scan to complete before returning. Returns 200 for clean files, 406 for infected files.")
            .DisableAntiforgery();

        // Asynchronous file upload scan
        scanGroup.MapPost("/async", async (IFormFile file, FileScanHandler handler) =>
            await handler.HandleAsyncAsync(file))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<AsyncScanResponse>(202)
            .Produces<ErrorResponse>(400)
            .WithName("ScanAsync")
            .WithDescription("Upload a file for asynchronous virus scanning. Returns immediately with a job ID.")
            .DisableAntiforgery();

        // Asynchronous URL scan
        scanGroup.MapPost("/async/url", async (ScanUrlRequest urlRequest, UrlScanHandler handler) =>
            await handler.HandleAsync(urlRequest))
            .Accepts<ScanUrlRequest>("application/json")
            .Produces<AsyncScanResponse>(202)
            .Produces<ErrorResponse>(400)
            .WithName("ScanAsyncUrl")
            .WithDescription("Download a file from a URL and scan it asynchronously. Returns immediately with job ID. Download and scan happen in background. Set 'isBase64' to true if the URL is Base64 encoded.");

        // JSON scan endpoint
        scanGroup.MapPost("/json", async (JsonScanRequest jsonRequest, JsonScanHandler handler) =>
            await handler.HandleAsync(jsonRequest))
            .Accepts<JsonScanRequest>("application/json")
            .Produces<JsonScanResult>(200)
            .Produces<JsonScanResult>(406)
            .Produces(500)
            .WithName("ScanJson")
            .WithDescription("Scan a JSON payload for malware. Automatically detects and decodes any base64-encoded properties, scans each decoded item plus the full JSON text. Perfect for Azure Logic Apps and Functions. Returns 200 for clean content, 406 for infected content.");

        // Check scan status
        scanGroup.MapGet("/async/{jobId}", (string jobId, IScanJobService jobService) =>
        {
            var job = jobService.GetJob(jobId);
            if (job == null)
            {
                return Results.NotFound(new ErrorResponse { Error = "Job not found" });
            }

            return Results.Ok(new ScanStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                FileName = job.FileName,
                FileSize = job.FileSize,
                Engine = job.Engine,
                Malware = job.Malware,
                Error = job.Error,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt,
                ScanDurationMs = job.ScanDuration?.TotalMilliseconds
            });
        })
        .Produces<ScanStatusResponse>(200)
        .Produces<ErrorResponse>(404)
        .WithName("GetScanStatus")
        .WithDescription("Get the status of an asynchronous scan job");

        // List all jobs
        scanGroup.MapGet("/jobs", (IScanJobService jobService) =>
        {
            var jobs = jobService.GetAllJobs().Take(100).ToList(); // Limit to 100 most recent
            return Results.Ok(new JobsListResponse
            {
                Jobs = jobs.Select(j => new JobSummary
                {
                    JobId = j.JobId,
                    Status = j.Status,
                    FileName = j.FileName,
                    FileSize = j.FileSize,
                    CreatedAt = j.CreatedAt,
                    CompletedAt = j.CompletedAt,
                    ScanDurationMs = j.ScanDuration?.TotalMilliseconds
                }),
                Count = jobs.Count
            });
        })
        .Produces<JobsListResponse>(200)
        .WithName("ListJobs")
        .WithDescription("List recent scan jobs (for monitoring/debugging)");
    }
}


