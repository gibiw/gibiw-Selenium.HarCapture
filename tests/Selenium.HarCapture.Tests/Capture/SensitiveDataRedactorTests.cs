using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactHeaders_WithMatchingName_ReplacesValue()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: new[] { "Authorization" },
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var headers = new List<HarHeader>
        {
            new() { Name = "Authorization", Value = "Bearer secret-token" },
            new() { Name = "Content-Type", Value = "application/json" }
        };

        // Act
        var result = redactor.RedactHeaders(headers);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Authorization");
        result[0].Value.Should().Be("[REDACTED]");
        result[1].Name.Should().Be("Content-Type");
        result[1].Value.Should().Be("application/json");
    }

    [Fact]
    public void RedactHeaders_CaseInsensitive_ReplacesValue()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: new[] { "Authorization" },
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var headers = new List<HarHeader>
        {
            new() { Name = "authorization", Value = "Bearer secret-token" },
            new() { Name = "AUTHORIZATION", Value = "Bearer another-token" }
        };

        // Act
        var result = redactor.RedactHeaders(headers);

        // Assert
        result.Should().HaveCount(2);
        result[0].Value.Should().Be("[REDACTED]");
        result[1].Value.Should().Be("[REDACTED]");
    }

    [Fact]
    public void RedactHeaders_NoMatch_PreservesOriginal()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: new[] { "Authorization" },
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var headers = new List<HarHeader>
        {
            new() { Name = "Content-Type", Value = "application/json" }
        };

        // Act
        var result = redactor.RedactHeaders(headers);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Content-Type");
        result[0].Value.Should().Be("application/json");
    }

    [Fact]
    public void RedactHeaders_EmptyConfig_PreservesAll()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var headers = new List<HarHeader>
        {
            new() { Name = "Authorization", Value = "Bearer secret-token" },
            new() { Name = "Content-Type", Value = "application/json" }
        };

        // Act
        var result = redactor.RedactHeaders(headers);

        // Assert
        result.Should().BeSameAs(headers);
    }

    [Fact]
    public void RedactCookies_WithMatchingName_ReplacesValue()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: new[] { "session_id" },
            sensitiveQueryParams: null);

        var cookies = new List<HarCookie>
        {
            new() { Name = "session_id", Value = "abc123def456" },
            new() { Name = "preferences", Value = "theme=dark" }
        };

        // Act
        var result = redactor.RedactCookies(cookies);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("session_id");
        result[0].Value.Should().Be("[REDACTED]");
        result[1].Name.Should().Be("preferences");
        result[1].Value.Should().Be("theme=dark");
    }

    [Fact]
    public void RedactCookies_CaseInsensitive_ReplacesValue()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: new[] { "Session_Id" },
            sensitiveQueryParams: null);

        var cookies = new List<HarCookie>
        {
            new() { Name = "session_id", Value = "abc123def456" },
            new() { Name = "SESSION_ID", Value = "xyz789ghi012" }
        };

        // Act
        var result = redactor.RedactCookies(cookies);

        // Assert
        result.Should().HaveCount(2);
        result[0].Value.Should().Be("[REDACTED]");
        result[1].Value.Should().Be("[REDACTED]");
    }

    [Fact]
    public void RedactQueryString_WildcardPattern_ReplacesMatching()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: new[] { "api_*" });

        var queryParams = new List<HarQueryString>
        {
            new() { Name = "api_key", Value = "secret123" },
            new() { Name = "api_secret", Value = "topsecret456" },
            new() { Name = "page", Value = "1" }
        };

        // Act
        var result = redactor.RedactQueryString(queryParams);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("api_key");
        result[0].Value.Should().Be("[REDACTED]");
        result[1].Name.Should().Be("api_secret");
        result[1].Value.Should().Be("[REDACTED]");
        result[2].Name.Should().Be("page");
        result[2].Value.Should().Be("1");
    }

    [Fact]
    public void RedactQueryString_ExactMatch_ReplacesValue()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: new[] { "token" });

        var queryParams = new List<HarQueryString>
        {
            new() { Name = "token", Value = "secret123" },
            new() { Name = "token_type", Value = "bearer" }
        };

        // Act
        var result = redactor.RedactQueryString(queryParams);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("token");
        result[0].Value.Should().Be("[REDACTED]");
        result[1].Name.Should().Be("token_type");
        result[1].Value.Should().Be("bearer");
    }

    [Fact]
    public void RedactQueryString_NullConfig_PreservesAll()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var queryParams = new List<HarQueryString>
        {
            new() { Name = "api_key", Value = "secret123" },
            new() { Name = "page", Value = "1" }
        };

        // Act
        var result = redactor.RedactQueryString(queryParams);

        // Assert
        result.Should().BeSameAs(queryParams);
    }

    [Fact]
    public void HasRedactions_WhenConfigured_ReturnsTrue()
    {
        // Arrange & Act
        var redactorWithHeaders = new SensitiveDataRedactor(
            sensitiveHeaders: new[] { "Authorization" },
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var redactorWithCookies = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: new[] { "session_id" },
            sensitiveQueryParams: null);

        var redactorWithQueryParams = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: new[] { "api_*" });

        // Assert
        redactorWithHeaders.HasRedactions.Should().BeTrue();
        redactorWithCookies.HasRedactions.Should().BeTrue();
        redactorWithQueryParams.HasRedactions.Should().BeTrue();
    }

    [Fact]
    public void HasRedactions_WhenEmpty_ReturnsFalse()
    {
        // Arrange & Act
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        // Assert
        redactor.HasRedactions.Should().BeFalse();
    }

    [Fact]
    public void RedactUrl_WithMatchingQueryParam_ReplacesInUrl()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: new[] { "token" });

        var url = "https://api.example.com/endpoint?token=secret123&page=1";

        // Act
        var result = redactor.RedactUrl(url);

        // Assert
        result.Should().Be("https://api.example.com/endpoint?token=[REDACTED]&page=1");
    }

    [Fact]
    public void RedactUrl_NoQueryString_ReturnsOriginal()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: new[] { "token" });

        var url = "https://api.example.com/endpoint";

        // Act
        var result = redactor.RedactUrl(url);

        // Assert
        result.Should().Be("https://api.example.com/endpoint");
    }

    [Fact]
    public void RedactUrl_NullConfig_ReturnsOriginal()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null);

        var url = "https://api.example.com/endpoint?token=secret123&page=1";

        // Act
        var result = redactor.RedactUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void RedactUrl_WithMultipleMatchingParams_RedactsAll()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: new[] { "api_*", "secret" });

        var url = "https://api.example.com/endpoint?api_key=key123&secret=pass456&page=1";

        // Act
        var result = redactor.RedactUrl(url);

        // Assert
        result.Should().Be("https://api.example.com/endpoint?api_key=[REDACTED]&secret=[REDACTED]&page=1");
    }
}
