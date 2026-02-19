using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.Tests.Serialization;

/// <summary>
/// Tests for DateTimeOffsetConverter ensuring ISO 8601 format and timezone preservation.
/// </summary>
public sealed class DateTimeOffsetConverterTests
{
    private sealed class TestWrapper
    {
        public DateTimeOffset Dt { get; set; }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateTimeOffsetConverter());
        return options;
    }

    [Fact]
    public void Write_PreservesTimezoneOffset()
    {
        // Arrange
        var dto = new DateTimeOffset(2009, 7, 24, 19, 20, 30, 450, TimeSpan.FromHours(1));
        var wrapper = new TestWrapper { Dt = dto };

        // Act
        var json = JsonSerializer.Serialize(wrapper, CreateOptions());

        // Assert
        // JSON may escape + as \u002B, so check for both
        json.Should().Match(j => j.Contains("+01:00") || j.Contains("\\u002B01:00"),
            "timezone offset should be preserved, not converted to UTC");
    }

    [Fact]
    public void Write_ProducesIso8601Format()
    {
        // Arrange
        var dto = new DateTimeOffset(2009, 7, 24, 19, 20, 30, 450, TimeSpan.FromHours(1));
        var wrapper = new TestWrapper { Dt = dto };

        // Act
        var json = JsonSerializer.Serialize(wrapper, CreateOptions());

        // Assert
        json.Should().MatchRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", "should match ISO 8601 format");
    }

    [Fact]
    public void Read_ParsesIso8601WithTimezone()
    {
        // Arrange
        var json = """{"Dt":"2009-07-24T19:20:30.4500000+01:00"}""";

        // Act
        var wrapper = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

        // Assert
        wrapper.Should().NotBeNull();
        wrapper!.Dt.Year.Should().Be(2009);
        wrapper.Dt.Month.Should().Be(7);
        wrapper.Dt.Day.Should().Be(24);
        wrapper.Dt.Hour.Should().Be(19);
        wrapper.Dt.Minute.Should().Be(20);
        wrapper.Dt.Second.Should().Be(30);
        wrapper.Dt.Offset.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Read_ParsesUtcFormat()
    {
        // Arrange
        var json = """{"Dt":"2009-07-24T18:20:30.4500000+00:00"}""";

        // Act
        var wrapper = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

        // Assert
        wrapper.Should().NotBeNull();
        wrapper!.Dt.Year.Should().Be(2009);
        wrapper.Dt.Month.Should().Be(7);
        wrapper.Dt.Day.Should().Be(24);
        wrapper.Dt.Hour.Should().Be(18);
        wrapper.Dt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Read_ThrowsOnInvalidFormat()
    {
        // Arrange
        var json = """{"Dt":"not-a-date"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

        // Assert
        act.Should().Throw<JsonException>()
            .WithMessage("*Unable to parse*");
    }

    [Fact]
    public void Read_ThrowsOnEmptyString()
    {
        // Arrange
        var json = """{"Dt":""}""";

        // Act
        var act = () => JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

        // Assert
        act.Should().Throw<JsonException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void RoundTrip_PreservesValue()
    {
        // Arrange
        var original = new DateTimeOffset(2009, 7, 24, 19, 20, 30, 450, TimeSpan.FromHours(2));
        var wrapper = new TestWrapper { Dt = original };

        // Act
        var json = JsonSerializer.Serialize(wrapper, CreateOptions());
        var deserialized = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Dt.Should().Be(original);
    }
}
