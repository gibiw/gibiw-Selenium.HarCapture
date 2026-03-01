using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;
using Xunit;

namespace Selenium.HarCapture.Tests.Models;

/// <summary>
/// Tests for HarLog._custom serialization and round-trip behavior.
/// </summary>
public sealed class HarLogTests
{
    private static HarLog BuildLog(IDictionary<string, object>? custom = null)
    {
        return new HarLog
        {
            Version = "1.2",
            Creator = new HarCreator { Name = "Test", Version = "1.0" },
            Entries = new List<HarEntry>(),
            Custom = custom
        };
    }

    [Fact]
    public void HarLog_Custom_SerializesToJson()
    {
        // Arrange
        var log = BuildLog(new Dictionary<string, object> { ["env"] = "prod" });
        var har = new Har { Log = log };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        json.Should().Contain("\"_custom\"");
        json.Should().Contain("\"env\"");
        json.Should().Contain("\"prod\"");
    }

    [Fact]
    public void HarLog_Custom_Null_OmittedFromJson()
    {
        // Arrange
        var log = BuildLog(custom: null);
        var har = new Har { Log = log };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        json.Should().NotContain("_custom");
    }

    [Fact]
    public void HarLog_Custom_RoundTrips()
    {
        // Arrange
        var custom = new Dictionary<string, object>
        {
            ["env"] = "staging",
            ["transactionId"] = "tx-123",
            ["retryCount"] = 3
        };
        var log = BuildLog(custom);
        var har = new Har { Log = log };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);
        var deserialized = HarSerializer.Deserialize(json);

        // Assert
        deserialized.Log.Custom.Should().NotBeNull();
        deserialized.Log.Custom!.Should().ContainKey("env");
        deserialized.Log.Custom.Should().ContainKey("transactionId");
        deserialized.Log.Custom.Should().ContainKey("retryCount");

        // Values will be JsonElement after round-trip
        var envValue = deserialized.Log.Custom["env"];
        envValue.Should().NotBeNull();
        envValue.ToString().Should().Contain("staging");
    }
}
