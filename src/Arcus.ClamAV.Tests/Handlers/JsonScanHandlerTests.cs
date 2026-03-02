using Arcus.ClamAV.Handlers;
using Arcus.ClamAV.Models;
using Arcus.ClamAV.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Arcus.ClamAV.Tests.Handlers;

public class JsonScanHandlerTests
{
    private readonly Mock<IJsonBase64ExtractorService> _extractorServiceMock;
    private readonly Mock<ISyncScanService> _syncScanServiceMock;
    private readonly Mock<ILogger<JsonScanHandler>> _loggerMock;
    private readonly JsonScanHandler _handler;

    public JsonScanHandlerTests()
    {
        _extractorServiceMock = new Mock<IJsonBase64ExtractorService>();
        _syncScanServiceMock = new Mock<ISyncScanService>();
        _loggerMock = new Mock<ILogger<JsonScanHandler>>();

        _handler = new JsonScanHandler(_extractorServiceMock.Object, _syncScanServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ShouldReturnInternalServerError()
    {
        // Act & Assert - should handle null gracefully
        var result = await _handler.HandleAsync(default);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task HandleAsync_WithNoBase64AndNoStrings_ShouldReturnResult()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { data = 123 });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        // Act
        var result = await _handler.HandleAsync(payload);

        // Assert
        result.ShouldNotBeNull();
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task HandleAsync_WithCleanBase64Item_ShouldCallService()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { file = "base64data" });
        

        var base64Item = new Base64Extract { Path = "file", DecodedContent = Encoding.UTF8.GetBytes("content") };
        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract> { base64Item });

        var scanResult = new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 };
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.Is<long>(l => l == base64Item.DecodedContent.Length)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleBase64Items_ShouldCallServiceForEach()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { files = new[] { "base64_1", "base64_2" } });
        

        var item1 = new Base64Extract { Path = "files[0]", DecodedContent = Encoding.UTF8.GetBytes("content1") };
        var item2 = new Base64Extract { Path = "files[1]", DecodedContent = Encoding.UTF8.GetBytes("content2") };

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract> { item1, item2 });

        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync((Stream s, long l) => new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 });

        // Act
        await _handler.HandleAsync(payload);

        // Assert - expects 2 base64 scans + 2 string scans (array elements)
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task HandleAsync_WithStringValues_ShouldCallServiceForEach()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { str1 = "text1", str2 = "text2" });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync((Stream s, long l) => new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 });

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WithEmptyStringValue_ShouldSkipIt()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { text = "" });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceOnly_ShouldSkipIt()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { text = "   \n\t  " });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNestedStructure_ShouldScanRecursively()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new
        {
            level1 = new
            {
                level2 = new { text = "content" }
            }
        });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        var scanResult = new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 };
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithJsonArray_ShouldScanEachElement()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { items = new[] { "item1", "item2", "item3" } });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync((Stream s, long l) => new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 });

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_WithBothBase64AndStrings_ShouldScanBoth()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { file = "base64", text = "content" });
        

        var base64Item = new Base64Extract { Path = "file", DecodedContent = Encoding.UTF8.GetBytes("data") };
        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract> { base64Item });

        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync((Stream s, long l) => new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 });

        // Act
        await _handler.HandleAsync(payload);

        // Assert - expects 1 base64 scan + 2 string scans (file and text properties)
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_WithUnicodeText_ShouldEncodeCorrectly()
    {
        // Arrange
        var unicodeText = "Hello 世界 🌍";
        var payload = JsonSerializer.SerializeToElement(new { text = unicodeText });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        var scanResult = new SyncScanResult { IsSuccess = true, Status = "clean", Malware = null, DurationMs = 1.0 };
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(scanResult);

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        var expectedBytes = Encoding.UTF8.GetByteCount(unicodeText);
        _syncScanServiceMock.Verify(
            s => s.ScanStreamAsync(It.IsAny<Stream>(), It.Is<long>(l => l == expectedBytes)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallExtractorService()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { data = "test" });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        // Act
        await _handler.HandleAsync(payload);

        // Assert
        _extractorServiceMock.Verify(
            s => s.ExtractBase64Properties(It.IsAny<JsonElement>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenClamAvServiceThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { text = "data" });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());

        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ThrowsAsync(new IOException("Connection failed"));

        // Act
        var result = await _handler.HandleAsync(payload);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task HandleAsync_WhenExtractorThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { data = "test" });
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Throws(new InvalidOperationException("Extract failed"));

        // Act
        var result = await _handler.HandleAsync(payload);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task HandleAsync_WithInfectedResult_ShouldReturnNotAcceptable()
    {
        // Arrange
        var payload = JsonSerializer.SerializeToElement(new { file = "infected" });
        

        var base64Item = new Base64Extract { Path = "file", DecodedContent = Encoding.UTF8.GetBytes("virus") };
        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract> { base64Item });

        var infectedResult = new SyncScanResult { IsSuccess = true, Status = "infected", Malware = "Win.Test.EICAR_HDB-1", DurationMs = 1.0 };
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .ReturnsAsync(infectedResult);

        // Act
        var result = await _handler.HandleAsync(payload);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(406);
    }

    [Fact]
    public async Task HandleAsync_WithEscapedStringContainingVirus_ShouldDetectMalware()
    {
        // Arrange - EICAR test string with escaped backslash (as it would appear in JSON)
        // The JSON string "X5O!P%@AP[4\\PZX54..." should be unescaped to "X5O!P%@AP[4\PZX54..." before scanning
        var jsonString = "{\"testclear\": \"X5O!P%@AP[4\\\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*\"}";
        var payload = JsonSerializer.Deserialize<JsonElement>(jsonString);
        

        _extractorServiceMock.Setup(s => s.ExtractBase64Properties(It.IsAny<JsonElement>()))
            .Returns(new List<Base64Extract>());
        _extractorServiceMock.Setup(s => s.IsBase64Path(It.IsAny<string>()))
            .Returns(false);

        // Capture the actual string content that gets scanned
        byte[]? scannedBytes = null;
        _syncScanServiceMock.Setup(s => s.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<long>()))
            .Callback<Stream, long>((stream, size) =>
            {
                scannedBytes = new byte[size];
                stream.Read(scannedBytes, 0, (int)size);
                stream.Position = 0; // Reset for actual scanning
            })
            .ReturnsAsync(new SyncScanResult 
            { 
                IsSuccess = true, 
                Status = "infected", 
                Malware = "Win.Test.EICAR_HDB-1", 
                DurationMs = 1.0 
            });

        // Act
        var result = await _handler.HandleAsync(payload);

        // Assert
        var httpResult = result as IStatusCodeHttpResult;
        httpResult.ShouldNotBeNull();
        httpResult.StatusCode.ShouldBe(406);

        // Verify that the scanned string was properly unescaped (contains single backslash, not double)
        scannedBytes.ShouldNotBeNull();
        var scannedString = Encoding.UTF8.GetString(scannedBytes);
        scannedString.ShouldContain("X5O!P%@AP[4\\PZX54"); // Single backslash (unescaped)
        scannedString.ShouldNotContain("X5O!P%@AP[4\\\\PZX54"); // Should NOT contain double backslash
    }
}


