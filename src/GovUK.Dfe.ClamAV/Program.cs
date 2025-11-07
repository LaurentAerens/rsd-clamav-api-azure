using GovUK.Dfe.ClamAV.Endpoints;
using GovUK.Dfe.ClamAV.Handlers;
using GovUK.Dfe.ClamAV.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

var maxFileSizeMb = int.TryParse(Environment.GetEnvironmentVariable("MAX_FILE_SIZE_MB"), out var m) ? m : 200;

// Configure Kestrel for better upload performance
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = (long)maxFileSizeMb * 1024 * 1024;
    options.Limits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(
        bytesPerSecond: 100,
        gracePeriod: TimeSpan.FromSeconds(10));
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
});

// Limit request body size to MAX_FILE_SIZE_MB
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = (long)maxFileSizeMb * 1024 * 1024;
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartHeadersLengthLimit = int.MaxValue;
    o.BufferBody = false; // Don't buffer in memory
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ClamAV Scan API",
        Version = "v1",
        Description = "API wrapper for ClamAV virus scanning with async job support"
    });
});

// Register services
builder.Services.AddSingleton<IClamAvInfoService, ClamAvInfoService>();
builder.Services.AddSingleton<IScanJobService, ScanJobService>();

// Register handlers
builder.Services.AddScoped<FileScanHandler>();
builder.Services.AddScoped<UrlScanHandler>();

// Create channel for background processing (bounded to prevent memory issues)
var scanChannel = Channel.CreateBounded<ScanRequest>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait
});
builder.Services.AddSingleton(scanChannel);

// Register background service
builder.Services.AddHostedService<BackgroundScanService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ClamAV Scan API v1");
});

// Map endpoints
app.MapHealthEndpoints();
app.MapScanEndpoints();

app.Run();