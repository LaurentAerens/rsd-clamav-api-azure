using Arcus.ClamAV.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;
using System.Reflection;

namespace Arcus.ClamAV.Tests.Services;

public class TelemetryServiceTests
{
    #region GetFileSizeCategory Tests - Line 226-237

    [Theory]
    [InlineData(512 * 1024, "Small (<1MB)")]           // 512 KB
    [InlineData(1 * 1024 * 1024, "Medium (1-10MB)")]   // 1 MB
    [InlineData(5 * 1024 * 1024, "Medium (1-10MB)")]   // 5 MB
    [InlineData(10 * 1024 * 1024, "Large (10-50MB)")]  // 10 MB
    [InlineData(25 * 1024 * 1024, "Large (10-50MB)")]  // 25 MB
    [InlineData(50 * 1024 * 1024, "VeryLarge (50-100MB)")]   // 50 MB
    [InlineData(75 * 1024 * 1024, "VeryLarge (50-100MB)")]   // 75 MB
    [InlineData(100 * 1024 * 1024, "Huge (>100MB)")]   // 100 MB
    [InlineData(200 * 1024 * 1024, "Huge (>100MB)")]   // 200 MB
    [InlineData(0, "Small (<1MB)")]                     // Edge case: 0 bytes
    [InlineData(1, "Small (<1MB)")]                     // Edge case: 1 byte
    public void GetFileSizeCategory_CategorizesCorrectly(long fileSize, string expectedCategory)
    {
        // Arrange
        var method = typeof(TelemetryService).GetMethod(
            "GetFileSizeCategory",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(long)],
            null);

        method.ShouldNotBeNull();

        // Act
        var result = method.Invoke(null, [fileSize]) as string;

        // Assert
        result.ShouldBe(expectedCategory);
    }

    #endregion

    #region GetThreatCategory Tests - Line 263-288

    [Theory]
    [InlineData("Eicar-Test-Signature", "Test")]
    [InlineData("EICAR-TEST-SIGNATURE", "Test")]
    [InlineData("eicar", "Test")]
    [InlineData("Trojan.Generic", "Trojan")]
    [InlineData("TROJAN_BACKDOOR", "Trojan")]
    [InlineData("Win32.Virus.Detected", "Virus")]
    [InlineData("VIRUS-ALERT", "Virus")]
    [InlineData("Email.Worm.Mass", "Worm")]
    [InlineData("WORM_EMAIL", "Worm")]
    [InlineData("Ransomware.Locky", "Ransomware")]
    [InlineData("RANSOMWARE_CRYPTO", "Ransomware")]
    [InlineData("Adware.ShowAds", "Spyware")]
    [InlineData("ADWARE_PUP", "Spyware")]
    [InlineData("Spyware.KeyLogger", "Spyware")]
    [InlineData("SPYWARE_TRACKING", "Spyware")]
    [InlineData("Rootkit.Hidden", "Rootkit")]
    [InlineData("ROOTKIT_KERNEL", "Rootkit")]
    [InlineData("Unknown.Threat", "Other")]
    [InlineData("NewMalwareType", "Other")]
    [InlineData("", "Other")]
    [InlineData("N/A", "Other")]
    public void GetThreatCategory_CategorizesThreatsCorrectly(string threatName, string expectedCategory)
    {
        // Arrange
        var method = typeof(TelemetryService).GetMethod(
            "GetThreatCategory",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(string)],
            null);

        method.ShouldNotBeNull();

        // Act
        var result = method.Invoke(null, [threatName]) as string;

        // Assert
        result.ShouldBe(expectedCategory);
    }

    #endregion

    #region GetErrorCategory Tests - Line 293-318

    [Theory]
    [InlineData("Connection timeout occurred", "Timeout")]
    [InlineData("Request timeout", "Timeout")]
    [InlineData("TIMEOUT ERROR", "Timeout")]
    [InlineData("Network error occurred", "Network")]
    [InlineData("Connection refused", "Network")]
    [InlineData("NETWORK_UNAVAILABLE", "Network")]
    [InlineData("File not found", "FileSystem")]
    [InlineData("Path does not exist", "FileSystem")]
    [InlineData("Out of memory", "Memory")]
    [InlineData("Connection timeout", "Timeout")]
    [InlineData("Network error", "Network")]
    [InlineData("Access denied", "Permission")]
    [InlineData("Parse error", "Format")]
    [InlineData("permission is denied", "Permission")]
    [InlineData("file cannot access", "FileSystem")]
    [InlineData("path invalid", "FileSystem")]
    [InlineData("connection refused", "Network")]
    [InlineData("memory exhausted", "Memory")]
    [InlineData("timeout expired", "Timeout")]
    [InlineData("Unknown error type", "Other")]
    [InlineData("Random error message", "Other")]
    [InlineData("", "Other")]
    public void GetErrorCategory_CategorizeErrorsCorrectly(string errorMessage, string expectedCategory)
    {
        // Arrange
        var method = typeof(TelemetryService).GetMethod(
            "GetErrorCategory",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(string)],
            null);

        method.ShouldNotBeNull();

        // Act
        var result = method.Invoke(null, [errorMessage]) as string;

        // Assert
        result.ShouldBe(expectedCategory);
    }

    #endregion

    #region SanitizeForLogging Tests - Line 242-258

    [Theory]
    [InlineData("normal filename.txt", "normal filename.txt")]
    [InlineData("file\nwith\nnewlines", "file with newlines")]
    [InlineData("file\r\nwith\r\ncrlf", "file with crlf")]
    [InlineData("file\twith\ttabs", "file with tabs")]
    [InlineData("file  with   multiple   spaces", "file with multiple spaces")]
    [InlineData("  leading and trailing  ", "leading and trailing")]
    [InlineData("\n\n\n", "")]
    [InlineData("", "")]
    [InlineData(null, null)] // Null passes through
    [InlineData("normal_file_name", "normal_file_name")]
    [InlineData("file-with-dashes", "file-with-dashes")]
    [InlineData("UPPERCASE_FILE", "UPPERCASE_FILE")]
    public void SanitizeForLogging_RemovesControlCharactersAndNormalizesWhitespace(string? input, string? expected)
    {
        // Arrange
        var method = typeof(TelemetryService).GetMethod(
            "SanitizeForLogging",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(string)],
            null);

        method.ShouldNotBeNull();

        // Act
        var result = method.Invoke(null, [input]) as string;

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SanitizeForLogging_PreventLogInjection()
    {
        // Arrange
        var method = typeof(TelemetryService).GetMethod(
            "SanitizeForLogging",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(string)],
            null);

        method.ShouldNotBeNull();

        // Act - Try to inject a fake log line via newline
        var maliciousInput = "innocentfile.txt\nERROR: Hacked!";
        var result = method.Invoke(null, [maliciousInput]) as string;

        // Assert - Should strip newline so fake ERROR entry can't be injected
        result.ShouldNotBeNull();
        result.ShouldBe("innocentfile.txt ERROR: Hacked!");
        result.ShouldNotContain("\n");
    }

    #endregion
}
