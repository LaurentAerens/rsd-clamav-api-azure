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
            // 1. Extract base64 items and string values
            var base64Items = extractorService.ExtractBase64Properties(request.Payload);
            var stringValues = ExtractStringValues(request.Payload);
            result.Base64ItemsFound = base64Items.Count;

            logger.LogInformation("Found {Base64Count} base64 items and {StringCount} string values in JSON payload", 
                base64Items.Count, stringValues.Count);

            // 2. Scan each base64-decoded item
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
                    var virusName = scanResult.InfectedFiles?.FirstOrDefault()?.VirusName ?? "unknown";
                    detail.Malware = virusName;
                    result.Status = "infected";
                    result.Malware = virusName;
                    result.InfectedItem = item.Path;

                    logger.LogWarning("Malware detected in base64 property '{Path}': {Virus}", item.Path, virusName);
                    result.Details.Add(detail);
                    return Results.Json(result, statusCode: 406);
                }

                result.Details.Add(detail);
            }

            // 3. Scan each plaintext string value
            foreach (var stringValue in stringValues)
            {
                if (string.IsNullOrWhiteSpace(stringValue.Content))
                    continue;

                var stringBytes = Encoding.UTF8.GetBytes(stringValue.Content);
                using var stringStream = new MemoryStream(stringBytes);
                clam.MaxStreamSize = stringBytes.Length;

                var scanResult = await clam.SendAndScanFileAsync(stringStream);
                result.ItemsScanned++;

                var detail = new ScannedItemDetail
                {
                    Name = stringValue.Path,
                    Type = "plaintext",
                    Size = stringBytes.Length,
                    Status = scanResult.Result == ClamScanResults.Clean ? "clean" : "infected"
                };

                if (scanResult.Result == ClamScanResults.VirusDetected)
                {
                    var virusName = scanResult.InfectedFiles?.FirstOrDefault()?.VirusName ?? "unknown";
                    detail.Malware = virusName;
                    result.Status = "infected";
                    result.Malware = virusName;
                    result.InfectedItem = stringValue.Path;

                    logger.LogWarning("Malware detected in plaintext property '{Path}': {Virus}", stringValue.Path, virusName);
                    result.Details.Add(detail);
                    return Results.Json(result, statusCode: 406);
                }

                result.Details.Add(detail);
            }

            result.ScanDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JSON scan");
            result.Status = "error";
            result.ScanDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return Results.Problem("Scan error: " + ex.Message, statusCode: 500);
        }
    }

    private List<(string Path, string Content)> ExtractStringValues(JsonElement element, string currentPath = "")
    {
        var strings = new List<(string, string)>();
        ExtractStringsRecursive(element, currentPath, strings);
        return strings;
    }

    private void ExtractStringsRecursive(JsonElement element, string currentPath, List<(string Path, string Content)> strings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = string.IsNullOrEmpty(currentPath)
                        ? property.Name
                        : $"{currentPath}.{property.Name}";
                    ExtractStringsRecursive(property.Value, propertyPath, strings);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var arrayPath = $"{currentPath}[{index}]";
                    ExtractStringsRecursive(item, arrayPath, strings);
                    index++;
                }
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    strings.Add((currentPath, stringValue));
                }
                break;

            default:
                break;
        }
    }
}
