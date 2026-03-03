using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using System.Net;

namespace Arcus.ClamAV.Handlers;

public class FileScanHandler(
    IScanJobService jobService,
    IScanProcessingService scanProcessing,
    IBackgroundTaskQueue backgroundService,
    ISyncScanService syncScanService)
{
    public async Task<IResult> HandleSyncAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new ErrorResponse { Error = "Missing or empty file" });
        }

        await using var stream = file.OpenReadStream();
        var scanResult = await syncScanService.ScanStreamAsync(stream, file.Length);

        return scanResult.Status switch
        {
            "clean" => Results.Ok(new ScanResponse
            {
                Status = "clean",
                Engine = "clamav",
                FileName = file.FileName,
                Size = file.Length,
                ScanDurationMs = scanResult.DurationMs
            }),
            "infected" => Results.Json(new ScanResponse
            {
                Status = "infected",
                Engine = "clamav",
                Malware = scanResult.Malware,
                FileName = file.FileName,
                Size = file.Length,
                ScanDurationMs = scanResult.DurationMs
            }, statusCode: (int)HttpStatusCode.NotAcceptable),
            _ => Results.Problem($"Scan error: {scanResult.Error}", statusCode: (int)HttpStatusCode.InternalServerError)
        };
    }

    public async Task<IResult> HandleAsyncAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new ErrorResponse { Error = "Missing or empty file" });
        }

        // Create job first
        var jobId = jobService.CreateJob(file.FileName, file.Length);

        // Save to temp file
        var fileName = Path.GetFileName(file.FileName);
        var tempPath = Path.Join(Path.GetTempPath(), $"clamav_{jobId}_{fileName}");

        try
        {
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            await file.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            jobService.UpdateJobStatus(jobId, "error", error: $"Failed to save file: {ex.Message}");
            return Results.Problem("Failed to process upload", statusCode: 500);
        }

        // Enqueue for background processing
        _ = backgroundService.EnqueueTask(async (ct) =>
        {
            return await scanProcessing.ProcessFileScanAsync(jobId, tempPath, ct);
        });

        return Results.Accepted($"/scan/async/{jobId}", new AsyncScanResponse
        {
            JobId = jobId,
            Status = "queued",
            Message = "File uploaded successfully. Use the jobId to check scan status.",
            StatusUrl = $"/scan/async/{jobId}"
        });
    }
}


