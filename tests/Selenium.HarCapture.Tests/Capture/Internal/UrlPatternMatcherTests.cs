using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class UrlPatternMatcherTests
{
    [Fact]
    public void NoPatterns_CapturesAll()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(null, null);

        // Act
        var result = matcher.ShouldCapture("https://example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IncludePattern_MatchesUrl()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(new[] { "https://api.example.com/**" }, null);

        // Act
        var result = matcher.ShouldCapture("https://api.example.com/users");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IncludePattern_RejectsNonMatchingUrl()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(new[] { "https://api.example.com/**" }, null);

        // Act
        var result = matcher.ShouldCapture("https://other.com/data");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ExcludePattern_RejectsMatchingUrl()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(null, new[] { "**/*.png" });

        // Act
        var result = matcher.ShouldCapture("https://example.com/logo.png");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ExcludePattern_AllowsNonMatchingUrl()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(null, new[] { "**/*.png" });

        // Act
        var result = matcher.ShouldCapture("https://example.com/data.json");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ExcludeTakesPrecedence_OverInclude()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(
            new[] { "https://example.com/**" },
            new[] { "**/*.png" });

        // Act
        var result = matcher.ShouldCapture("https://example.com/image.png");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MultipleIncludePatterns_AnyMatch()
    {
        // Arrange
        var matcher = new UrlPatternMatcher(
            new[] { "https://api.example.com/**", "https://cdn.example.com/**" },
            null);

        // Act & Assert
        matcher.ShouldCapture("https://api.example.com/users").Should().BeTrue();
        matcher.ShouldCapture("https://cdn.example.com/assets/logo.png").Should().BeTrue();
        matcher.ShouldCapture("https://other.com/data").Should().BeFalse();
    }

    [Fact]
    public void CaptureAll_CapturesEverything()
    {
        // Arrange
        var matcher = UrlPatternMatcher.CaptureAll;

        // Act & Assert
        matcher.ShouldCapture("https://example.com").Should().BeTrue();
        matcher.ShouldCapture("https://api.example.com/users").Should().BeTrue();
        matcher.ShouldCapture("https://cdn.example.com/image.png").Should().BeTrue();
        matcher.ShouldCapture("http://localhost:8080/test").Should().BeTrue();
    }
}
