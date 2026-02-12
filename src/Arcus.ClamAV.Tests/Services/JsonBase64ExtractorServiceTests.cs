using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Arcus.ClamAV.Tests.Services;

public class JsonBase64ExtractorServiceTests
{
    private readonly ILogger<JsonBase64ExtractorService> _logger;
    private readonly JsonBase64ExtractorService _service;

    public JsonBase64ExtractorServiceTests()
    {
        _logger = new Mock<ILogger<JsonBase64ExtractorService>>().Object;
        _service = new JsonBase64ExtractorService(_logger);
    }

    [Fact]
    public void ExtractBase64Properties_WithSingleBase64Property_ShouldExtract()
    {
        // Arrange
        var originalContent = new byte[200]; // Create content > 100 bytes minimum
        Random.Shared.NextBytes(originalContent);
        var base64 = Convert.ToBase64String(originalContent);
        var json = JsonSerializer.Serialize(new { content = base64 });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.Count.ShouldBe(1);
        extracts[0].Path.ShouldBe("content");
        extracts[0].DecodedContent.ShouldBeEquivalentTo(originalContent);
    }

    [Fact]
    public void ExtractBase64Properties_WithMultipleBase64Properties_ShouldExtractAll()
    {
        // Arrange
        var content1 = new byte[200];
        var content2 = new byte[200];
        Random.Shared.NextBytes(content1);
        Random.Shared.NextBytes(content2);
        
        var json = JsonSerializer.Serialize(new
        {
            file1 = Convert.ToBase64String(content1),
            file2 = Convert.ToBase64String(content2)
        });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.Count.ShouldBe(2);
        extracts.ShouldContain(e => e.Path == "file1");
        extracts.ShouldContain(e => e.Path == "file2");
    }

    [Fact]
    public void ExtractBase64Properties_WithNestedBase64_ShouldExtractWithPath()
    {
        // Arrange
        var content = new byte[200];
        Random.Shared.NextBytes(content);
        
        var json = JsonSerializer.Serialize(new
        {
            envelope = new
            {
                body = new
                {
                    attachment = Convert.ToBase64String(content)
                }
            }
        });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.Count.ShouldBe(1);
        extracts[0].Path.ShouldBe("envelope.body.attachment");
    }

    [Fact]
    public void ExtractBase64Properties_WithArrayBase64_ShouldExtractWithIndex()
    {
        // Arrange
        var content1 = new byte[200];
        var content2 = new byte[200];
        Random.Shared.NextBytes(content1);
        Random.Shared.NextBytes(content2);
        
        var json = JsonSerializer.Serialize(new
        {
            attachments = new[]
            {
                new { data = Convert.ToBase64String(content1) },
                new { data = Convert.ToBase64String(content2) }
            }
        });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.Count.ShouldBe(2);
        extracts.ShouldContain(e => e.Path == "attachments[0].data");
        extracts.ShouldContain(e => e.Path == "attachments[1].data");
    }

    [Fact]
    public void ExtractBase64Properties_WithShortBase64_ShouldIgnore()
    {
        // Arrange - Base64 shorter than minimum (100 chars)
        var json = JsonSerializer.Serialize(new { content = "SGVsbG8=" }); // "Hello" in base64 (< 100 chars)
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.ShouldBeEmpty(); // Too short to be considered
    }

    [Fact]
    public void ExtractBase64Properties_WithPlainText_ShouldNotExtract()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { message = "This is just plain text, not base64!" });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractBase64Properties_WithInvalidBase64_ShouldSkip()
    {
        // Arrange - String that looks like base64 but isn't valid
        var invalidBase64 = new string('A', 200) + "!!!"; // Valid length but invalid chars
        var json = JsonSerializer.Serialize(new { content = invalidBase64 });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractBase64Properties_WithMixedContent_ShouldExtractOnlyBase64()
    {
        // Arrange
        var content = new byte[200];
        Random.Shared.NextBytes(content);
        
        var json = JsonSerializer.Serialize(new
        {
            id = "12345",
            message = "Plain text message",
            count = 42,
            isActive = true,
            fileContent = Convert.ToBase64String(content),
            tags = new[] { "tag1", "tag2" }
        });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.Count.ShouldBe(1);
        extracts[0].Path.ShouldBe("fileContent");
    }

    [Fact]
    public void ExtractBase64Properties_WithEmptyJson_ShouldReturnEmpty()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { });
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var extracts = _service.ExtractBase64Properties(jsonElement);

        // Assert
        extracts.ShouldBeEmpty();
    }
}
