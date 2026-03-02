using System.Text.Json;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Represents a base64-encoded item found in JSON.
/// </summary>
public class Base64Extract
{
    /// <summary>
    /// Path to the property (e.g., "content", "attachment.data", "items[0].file")
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// The decoded binary content
    /// </summary>
    public required byte[] DecodedContent { get; set; }
}

/// <summary>
/// Service for extracting and decoding base64 content from JSON payloads.
/// </summary>
public interface IJsonBase64ExtractorService
{
    /// <summary>
    /// Recursively searches JSON for base64-encoded string properties and extracts them.
    /// </summary>
    /// <param name="jsonElement">The JSON element to search</param>
    /// <returns>List of base64 items found and decoded</returns>
    List<Base64Extract> ExtractBase64Properties(JsonElement jsonElement);

    /// <summary>
    /// Check if a path was identified as containing base64 content.
    /// Must be called after ExtractBase64Properties.
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>True if path was identified as base64 during extraction</returns>
    bool IsBase64Path(string path);
}
