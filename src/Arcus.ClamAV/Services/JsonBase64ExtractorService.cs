using System.Text.Json;
using System.Text.RegularExpressions;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Service for extracting and decoding base64 content from JSON payloads.
/// </summary>
public partial class JsonBase64ExtractorService : IJsonBase64ExtractorService
{
    private readonly ILogger<JsonBase64ExtractorService> _logger;
    private const int MinBase64Length = 20; // Minimum length to consider as potential base64 content

    public JsonBase64ExtractorService(ILogger<JsonBase64ExtractorService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<Base64Extract> ExtractBase64Properties(JsonElement jsonElement)
    {
        var extracts = new List<Base64Extract>();
        ExtractRecursive(jsonElement, "", extracts);
        return extracts;
    }

    private void ExtractRecursive(JsonElement element, string currentPath, List<Base64Extract> extracts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = string.IsNullOrEmpty(currentPath)
                        ? property.Name
                        : $"{currentPath}.{property.Name}";
                    ExtractRecursive(property.Value, propertyPath, extracts);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var arrayPath = $"{currentPath}[{index}]";
                    ExtractRecursive(item, arrayPath, extracts);
                    index++;
                }
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (!string.IsNullOrEmpty(stringValue) && IsLikelyBase64(stringValue))
                {
                    try
                    {
                        // Remove whitespace and try to decode
                        var cleaned = stringValue.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
                        var decoded = Convert.FromBase64String(cleaned);

                        // Only consider it if decoded to reasonable size (avoid false positives)
                        if (decoded.Length >= 10)
                        {
                            _logger.LogInformation("Found base64 content at path '{Path}': {OriginalSize} bytes → {DecodedSize} bytes",
                                currentPath, stringValue.Length, decoded.Length);

                            extracts.Add(new Base64Extract
                            {
                                Path = currentPath,
                                DecodedContent = decoded
                            });
                        }
                    }
                    catch (FormatException)
                    {
                        // Not valid base64, skip
                        _logger.LogDebug("Property '{Path}' looked like base64 but failed to decode", currentPath);
                    }
                }
                break;

            // Other types (number, bool, null) are not base64
            default:
                break;
        }
    }

    private static bool IsLikelyBase64(string value)
    {
        // Must be long enough
        if (value.Length < MinBase64Length)
        {
            return false;
        }

        // Remove whitespace
        var cleaned = value.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");

        // Must be valid base64 length (multiple of 4, or with padding)
        if (cleaned.Length % 4 != 0)
        {
            return false;
        }

        // Check if it's mostly base64 characters
        var base64Regex = Base64Pattern();
        return base64Regex.IsMatch(cleaned);
    }

    [GeneratedRegex(@"^[A-Za-z0-9+/]*={0,2}$", RegexOptions.Compiled)]
    private static partial Regex Base64Pattern();
}
