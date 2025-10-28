using GovUK.Dfe.ClamAV.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using nClam;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var maxFileSizeMb = int.TryParse(Environment.GetEnvironmentVariable("MAX_FILE_SIZE_MB"), out var m) ? m : 200;

// Limit request body size to MAX_FILE_SIZE_MB
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = (long)maxFileSizeMb * 1024 * 1024;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ClamAV Scan API",
        Version = "v1",
        Description = "API wrapper for ClamAV virus scanning"
    });
});

builder.Services.AddSingleton<IClamAvInfoService, ClamAvInfoService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ClamAV Scan API v1");
});


app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/version", async (IClamAvInfoService clam) =>
{
    var version = await clam.GetVersionAsync();
    return Results.Ok(new { clamavVersion = version });
});

app.MapPost("/scan", async (IFormFile file) =>
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest(new { error = "Missing or empty file" });

        var host = Environment.GetEnvironmentVariable("CLAMD_HOST") ?? "127.0.0.1";
        var port = int.TryParse(Environment.GetEnvironmentVariable("CLAMD_PORT"), out var p) ? p : 3310;

        var clam = new ClamClient(host, port) { MaxStreamSize = file.Length }; // let clamd enforce limits too

        await using var stream = file.OpenReadStream();
        var result = await clam.SendAndScanFileAsync(stream);

        return result.Result switch
        {
            ClamScanResults.Clean => Results.Ok(new
            {
                status = "clean",
                engine = "clamav",
                signatureDbTime = DateTimeOffset.UtcNow,
                fileName = file.FileName,
                size = file.Length
            }),
            ClamScanResults.VirusDetected => Results.Ok(new
            {
                status = "infected",
                engine = "clamav",
                malware = result.InfectedFiles.FirstOrDefault()?.VirusName ?? "unknown",
                fileName = file.FileName,
                size = file.Length
            }),
            _ => Results.Problem(new
            {
                status = "error",
                engine = "clamav",
                raw = result.RawResult
            }.ToString(), statusCode: (int)HttpStatusCode.InternalServerError)
        };
    })
.Accepts<IFormFile>("multipart/form-data")
.Produces(200)
.Produces(400)
.Produces(500)
.DisableAntiforgery();


app.Run();