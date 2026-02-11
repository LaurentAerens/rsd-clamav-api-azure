using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using nClam;
using System.Text;
using System.Text.Json;

namespace Arcus.ClamAV.Handlers;

public class JsonScanHandler(
    IJsonBase64ExtractorService extractorService,
    IConfiguration configuration,
    ILogger<JsonScanHandler> logger)
{
    public async Task<IResult> HandleAsync(JsonScanRequest request)
    {
        var startTime = DateTime.UtcNow;
        var host = configuration["CLAMD_HOST"] ?? Environment.GetEnvironmentVariable("CLAMD_HOST") ?? "127.0.0.1";
        var port = int.TryParse(configuration["CLAMD_PORT"] ?? Environment.GetEnvironmentVariable("CLAMD_PORT"), out var p) ? p : 3310;

        var clam = new ClamClient(host, port);
        var result = new JsonScanResult
        {
            Status = "clean",
            ItemsScanned = 0,
            Base64ItemsFound = 0,
            ScanDurationMs = 0
        };

        try
        {
            // 1. Extract base64 items from JSON
            var base64Items = extractorService.ExtractBase64Properties(request.Payload);
            result.Base64ItemsFound = base64Items.Count;

            logger.LogInformation("Found {Count} base64 items in JSON payload", base64Items.Count);

            // 2. Scan each base64 decoded item
            foreach (var item in base64Items)
            {
                using var memoryStream = new MemoryStream(item.DecodedContent);
                clam.MaxStreamSize = item.DecodedContent.Length;
                
                var scanResult = await clam.SendAndScanFileAsync(memoryStream);
                result.ItemsScanned++;

                var detail = new ScannedItemDetail
                {
                    Name = item.Path,
                    Type = "base64_decoded",
                    Size = item.DecodedContent.Length,
                    Status = scanResult.Result == ClamScanResults.Clean ? "clean" : "infected"
                };

                if (scanResult.Result == ClamScanResults.VirusDetected)
                {
                    var virusName = scanResult.InfectedFiles.FirstOrDefault()?.VirusName ?? "unknown";
                    detail.Malware = virusName;
                    result.Status = "infected";
                    result.Malware = virusName;
                    result.InfectedItem = item.Path;
                    
                    logger.LogWarning("Malware detected in base64 property '{Path}': {Virus}", item.Path, virusName);
                }

                result.Details.Add(detail);

                // Stop scanning if we found malware
                if (result.Status == "infected")
                    break;
            }

            // 3. Also scan the full JSON as text (only if nothing infected yet)
            if (result.Status == "clean")
            {
                var jsonText = JsonSerializer.Serialize(request.Payload);
                var jsonBytes = Encoding.UTF8.GetBytes(jsonText);
                
                using var jsonStream = new MemoryStream(jsonBytes);
                clam.MaxStreamSize = jsonBytes.Length;
                
                var scanResult = await clam.SendAndScanFileAsync(jsonStream);
                result.ItemsScanned++;

                var detail = new ScannedItemDetail
                {
                    Name = "json_payload",
                    Type = "json_text",
                    Size = jsonBytes.Length,
                    Status = scanResult.Result == ClamScanResults.Clean ? "clean" : "infected"
                };

                if (scanResult.Result == ClamScanResults.VirusDetected)
                {
                    var virusName = scanResult.InfectedFiles.FirstOrDefault()?.VirusName ?? "unknown";
                    detail.Malware = virusName;
                    result.Status = "infected";
                    result.Malware = virusName;
                    result.InfectedItem = "json_payload";
                    
                    logger.LogWarning("Malware detected in JSON payload text: {Virus}", virusName);
                }

                result.Details.Add(detail);
            }

            result.ScanDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return result.Status == "infected" 
                ? Results.Json(result, statusCode: 406) 
                : Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JSON scan");
            result.Status = "error";
            result.ScanDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return Results.Problem("Scan error: " + ex.Message, statusCode: 500);
        }
    }
}
