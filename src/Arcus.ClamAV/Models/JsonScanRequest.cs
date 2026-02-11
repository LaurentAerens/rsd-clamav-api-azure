using System.Text.Json;

namespace Arcus.ClamAV.Models;

/// <summary>
/// Request model for scanning JSON payloads that may contain base64-encoded content.
/// </summary>
public class JsonScanRequest
{
    /// <summary>
    /// The JSON payload to scan. Can contain embedded base64 content in any property.
    /// </summary>
    public JsonElement Payload { get; set; }
}
