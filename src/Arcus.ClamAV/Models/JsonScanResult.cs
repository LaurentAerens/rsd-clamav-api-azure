namespace Arcus.ClamAV.Models;

/// <summary>
/// Result from scanning a JSON payload.
/// </summary>
public class JsonScanResult
{
    /// <summary>
    /// Overall status: "clean", "infected", or "error"
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Total number of items scanned (JSON text + decoded base64 items)
    /// </summary>
    public int ItemsScanned { get; set; }

    /// <summary>
    /// Number of base64-encoded items found and decoded
    /// </summary>
    public int Base64ItemsFound { get; set; }

    /// <summary>
    /// Malware name if infected
    /// </summary>
    public string? Malware { get; set; }

    /// <summary>
    /// Which item was infected (e.g., "json_payload", "property: content")
    /// </summary>
    public string? InfectedItem { get; set; }

    /// <summary>
    /// Total scan duration in milliseconds
    /// </summary>
    public double ScanDurationMs { get; set; }

    /// <summary>
    /// Details about each scanned item
    /// </summary>
    public List<ScannedItemDetail> Details { get; set; } = new();
}

/// <summary>
/// Details about a single scanned item within the JSON payload.
/// </summary>
public class ScannedItemDetail
{
    /// <summary>
    /// Name or path of the item (e.g., "json_payload", "content", "attachments[0].data")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of item: "json_text", "base64_decoded"
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Scan result: "clean", "infected"
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Malware name if infected
    /// </summary>
    public string? Malware { get; set; }
}
