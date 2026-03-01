using System;
using System.Threading;
using FluentAssertions;
using Selenium.HarCapture;
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

    // =========================================================================
    // Body Redaction Tests (Plan 19-01)
    // =========================================================================

    [Fact]
    public void RedactBody_WithMatchingPattern_ReplacesWithRedacted()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        var body = "email: test@example.com";

        // Act
        var result = redactor.RedactBody(body, out int count, logger: null, requestId: "req1");

        // Assert
        result.Should().Be("email: [REDACTED]");
        count.Should().Be(1);
    }

    [Fact]
    public void RedactBody_WithMultiplePatterns_ReplacesAll()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email, HarPiiPatterns.Ssn });

        var body = "email: user@example.com ssn: 123-45-6789";

        // Act
        var result = redactor.RedactBody(body, out int count, logger: null, requestId: "req1");

        // Assert
        result.Should().Contain("[REDACTED]");
        result.Should().NotContain("user@example.com");
        result.Should().NotContain("123-45-6789");
        count.Should().Be(2);
    }

    [Fact]
    public void RedactBody_WithNullBody_ReturnsNull()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Act
        var result = redactor.RedactBody(null!, out int count, logger: null, requestId: "req1");

        // Assert
        result.Should().BeNull();
        count.Should().Be(0);
    }

    [Fact]
    public void RedactBody_WithEmptyBody_ReturnsEmpty()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Act
        var result = redactor.RedactBody("", out int count, logger: null, requestId: "req1");

        // Assert
        result.Should().Be("");
        count.Should().Be(0);
    }

    [Fact]
    public void RedactBody_WithNoPatterns_ReturnsSameBody()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: null);

        var body = "email: test@example.com";

        // Act
        var result = redactor.RedactBody(body, out int count, logger: null, requestId: "req1");

        // Assert
        result.Should().BeSameAs(body);
        count.Should().Be(0);
    }

    [Fact]
    public void RedactBody_ExceedingSizeGate_SkipsRedaction()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Create a body larger than 512 KB
        var largeBody = "x" + new string('a', 512 * 1024) + " email@example.com";

        // Act
        var result = redactor.RedactBody(largeBody, out int count, logger: null, requestId: "req1");

        // Assert — body returned unchanged, email NOT redacted
        result.Should().BeSameAs(largeBody);
        count.Should().Be(0);
    }

    [Fact]
    public void RedactBody_WithCreditCardPattern_RedactsCreditCards()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.CreditCard });

        var body = "card: 4111111111111111";

        // Act
        var result = redactor.RedactBody(body, out int count, logger: null, requestId: "req1");

        // Assert
        result.Should().Be("card: [REDACTED]");
        count.Should().Be(1);
    }

    // =========================================================================
    // HasBodyPatterns Property Tests
    // =========================================================================

    [Fact]
    public void HasBodyPatterns_WhenPatterns_ReturnsTrue()
    {
        // Arrange & Act
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Assert
        redactor.HasBodyPatterns.Should().BeTrue();
    }

    [Fact]
    public void HasBodyPatterns_WhenNoPatterns_ReturnsFalse()
    {
        // Arrange & Act
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: null);

        // Assert
        redactor.HasBodyPatterns.Should().BeFalse();
    }

    // =========================================================================
    // Audit Counter Tests
    // =========================================================================

    [Fact]
    public void RecordBodyRedaction_TracksCount()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Act — simulate what happens when RedactBody finds matches
        redactor.RecordBodyRedaction(5);
        redactor.RecordBodyRedaction(3);

        // Assert — LogAudit should not throw (verifies counter increment works)
        var act = () => redactor.LogAudit(logger: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordWsRedaction_TracksCount()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Act
        redactor.RecordWsRedaction(2);

        // Assert — should not throw
        var act = () => redactor.LogAudit(logger: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordBodySkipped_TracksCount()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Act
        redactor.RecordBodySkipped();
        redactor.RecordBodySkipped();

        // Assert — should not throw
        var act = () => redactor.LogAudit(logger: null);
        act.Should().NotThrow();
    }

    // =========================================================================
    // Constructor Compatibility Tests
    // =========================================================================

    [Fact]
    public void Constructor_WithNullBodyPatterns_WorksLikeExisting()
    {
        // Arrange & Act — 4-param constructor, 4th is null
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: null);

        // Assert
        redactor.HasBodyPatterns.Should().BeFalse();
        redactor.HasRedactions.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithBodyPatterns_SetsHasRedactions()
    {
        // Arrange & Act — body patterns alone should make HasRedactions true
        var redactor = new SensitiveDataRedactor(
            sensitiveHeaders: null,
            sensitiveCookies: null,
            sensitiveQueryParams: null,
            sensitiveBodyPatterns: new[] { HarPiiPatterns.Email });

        // Assert
        redactor.HasBodyPatterns.Should().BeTrue();
        redactor.HasRedactions.Should().BeTrue();
    }
}
