using System.Text;
using System.Text.Json;

namespace Arcus.ClamAV.Benchmarks.TestData;

/// <summary>
/// Generates test JSON payloads for benchmarking.
/// Creates large payloads with sparse base64 fields and plaintext content.
/// </summary>
public static class TestPayloadGenerator
{
    private const string EICAR = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    
    /// <summary>
    /// Create a large clean JSON payload (200-500KB) with sparse base64 fields.
    /// </summary>
    public static JsonElement CreateLargeCleanPayload()
    {
        var dict = new Dictionary<string, object>
        {
            { "metadata", new
            {
                requestId = Guid.NewGuid().ToString(),
                timestamp = DateTime.UtcNow,
                userId = Guid.NewGuid().ToString(),
                source = "benchmark"
            }},
            { "content", GenerateLargeText(300_000) }, // ~300KB plaintext
            { "document_base64_1", Convert.ToBase64String(Encoding.UTF8.GetBytes("Clean document content 1"))},
            { "description", GenerateLargeText(100_000) }, // ~100KB plaintext
            { "attachment_base64_2", Convert.ToBase64String(Encoding.UTF8.GetBytes("Clean attachment content 2"))},
            { "notes", GenerateLargeText(50_000) }, // ~50KB plaintext
            { "data_items", new object[]
            {
                new { id = 1, data = GenerateMediumText(10_000), base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes("Item 1 data")) },
                new { id = 2, data = GenerateMediumText(10_000), base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes("Item 2 data")) },
                new { id = 3, data = GenerateMediumText(10_000), base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes("Item 3 data")) },
            }}
        };

        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(dict, options);
        return JsonSerializer.Deserialize<JsonElement>(json)!;
    }

    /// <summary>
    /// Create a mixed payload with clean and infected base64 + plaintext.
    /// </summary>
    public static JsonElement CreateMixedPayload()
    {
        var dict = new Dictionary<string, object>
        {
            { "metadata", new
            {
                requestId = Guid.NewGuid().ToString(),
                timestamp = DateTime.UtcNow,
            }},
            { "content", GenerateLargeText(150_000) }, // plaintext
            { "clean_attachment_base64_1", Convert.ToBase64String(Encoding.UTF8.GetBytes("This is clean data"))},
            { "description", GenerateMediumText(50_000) }, // plaintext
            { "infected_attachment_base64_2", Convert.ToBase64String(Encoding.UTF8.GetBytes(EICAR))},
            { "more_plaintext", GenerateMediumText(30_000) },
            { "clean_attachment_base64_3", Convert.ToBase64String(Encoding.UTF8.GetBytes("Another clean file"))}
        };

        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(dict, options);
        return JsonSerializer.Deserialize<JsonElement>(json)!;
    }

    /// <summary>
    /// Create a clean payload (small for quick benchmarks).
    /// </summary>
    public static JsonElement CreateSmallCleanPayload()
    {
        var dict = new Dictionary<string, object>
        {
            { "id", Guid.NewGuid().ToString() },
            { "title", "Test document" },
            { "content", GenerateMediumText(5_000) },
            { "attachment_base64", Convert.ToBase64String(Encoding.UTF8.GetBytes("Small clean data")) },
            { "description", "A small test payload for quick benchmarks" }
        };

        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(dict, options);
        return JsonSerializer.Deserialize<JsonElement>(json)!;
    }

    /// <summary>
    /// Get the JSON as a string.
    /// </summary>
    public static string GetPayloadString(JsonElement payload)
    {
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Generate large plaintext content (approximately the specified byte size).
    /// </summary>
    private static string GenerateLargeText(int approximateSize)
    {
        var sb = new StringBuilder();
        var sampleText = "The quick brown fox jumps over the lazy dog. This is a sample text for benchmarking purposes. ";
        var repetitions = (int)Math.Ceiling((double)approximateSize / sampleText.Length);
        
        for (int i = 0; i < repetitions; i++)
        {
            sb.Append(sampleText);
            if (i % 100 == 0)
                sb.Append(Guid.NewGuid().ToString()).Append(" ");
        }

        return sb.ToString().Substring(0, approximateSize);
    }

    /// <summary>
    /// Generate medium plaintext content (approximately the specified byte size).
    /// </summary>
    private static string GenerateMediumText(int approximateSize)
    {
        var sampleText = $"Item data: {Guid.NewGuid()} ";
        var repetitions = (int)Math.Ceiling((double)approximateSize / sampleText.Length);
        return string.Concat(Enumerable.Range(0, repetitions).Select(_ => sampleText)).Substring(0, Math.Min(approximateSize, approximateSize));
    }
}
