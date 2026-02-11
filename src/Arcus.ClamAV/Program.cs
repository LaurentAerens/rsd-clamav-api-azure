using Arcus.ClamAV.Endpoints;
using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure to read from appsettings.json and environment-specific files
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Configure Application Insights telemetry (only if connection string provided)
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] 
                                   ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
        options.EnableAdaptiveSampling = true;
        options.EnablePerformanceCounterCollectionModule = true;
        options.EnableDependencyTrackingTelemetryModule = true;
    });
    
    builder.Logging.AddApplicationInsights();
}

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

builder.Services.AddOpenApiDocument(configure => { configure.Title = "ClamAv Api"; });

// Register services
builder.Services.AddSingleton<IClamAvInfoService, ClamAvInfoService>();
builder.Services.AddSingleton<IScanJobService, ScanJobService>();
builder.Services.AddSingleton<IJsonBase64ExtractorService, JsonBase64ExtractorService>();

// Register telemetry service (safe to use even if Application Insights is not configured)
builder.Services.AddSingleton<ITelemetryService, TelemetryService>();

// Register processing service
builder.Services.AddScoped<IScanProcessingService, ScanProcessingService>();

// Register handlers
builder.Services.AddScoped<FileScanHandler>();
builder.Services.AddScoped<UrlScanHandler>();
builder.Services.AddScoped<JsonScanHandler>();

// Add background task queue with 4 concurrent workers
builder.Services.AddSingleton<IBackgroundTaskQueue>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<BackgroundTaskQueue>>();
    var telemetryService = sp.GetRequiredService<ITelemetryService>();
    return new BackgroundTaskQueue(capacity: 100, logger, telemetryService);
});
builder.Services.AddHostedService<QueuedHostedService>();

// Register job cleanup service
builder.Services.AddHostedService<JobCleanupService>();

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
