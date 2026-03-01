using System.Text.RegularExpressions;
using FluentAssertions;
using Selenium.HarCapture;
using Selenium.HarCapture.Capture;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public sealed class RedactionIntegrationTests
{
    [Fact]
    public void CaptureOptions_WithSensitiveHeaders_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveHeaders("Authorization", "X-Api-Key");

        // Assert
        options.SensitiveHeaders.Should().NotBeNull();
        options.SensitiveHeaders.Should().HaveCount(2);
        options.SensitiveHeaders.Should().Contain("Authorization");
        options.SensitiveHeaders.Should().Contain("X-Api-Key");
    }

    [Fact]
    public void CaptureOptions_WithSensitiveCookies_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveCookies("session_id", "auth_token");

        // Assert
        options.SensitiveCookies.Should().NotBeNull();
        options.SensitiveCookies.Should().HaveCount(2);
        options.SensitiveCookies.Should().Contain("session_id");
        options.SensitiveCookies.Should().Contain("auth_token");
    }

    [Fact]
    public void CaptureOptions_WithSensitiveQueryParams_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveQueryParams("api_*", "token");

        // Assert
        options.SensitiveQueryParams.Should().NotBeNull();
        options.SensitiveQueryParams.Should().HaveCount(2);
        options.SensitiveQueryParams.Should().Contain("api_*");
        options.SensitiveQueryParams.Should().Contain("token");
    }

    [Fact]
    public void CaptureOptions_FluentChaining_Works()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveHeaders("Authorization")
            .WithSensitiveCookies("session_id")
            .WithSensitiveQueryParams("api_*")
            .WithMaxResponseBodySize(1024000);

        // Assert
        options.SensitiveHeaders.Should().NotBeNull();
        options.SensitiveHeaders.Should().Contain("Authorization");
        options.SensitiveCookies.Should().NotBeNull();
        options.SensitiveCookies.Should().Contain("session_id");
        options.SensitiveQueryParams.Should().NotBeNull();
        options.SensitiveQueryParams.Should().Contain("api_*");
        options.MaxResponseBodySize.Should().Be(1024000);
    }

    [Fact]
    public void CaptureOptions_Defaults_RedactionPropertiesAreNull()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.SensitiveHeaders.Should().BeNull();
        options.SensitiveCookies.Should().BeNull();
        options.SensitiveQueryParams.Should().BeNull();
    }

    // =========================================================================
    // CaptureOptions.WithSensitiveBodyPatterns Tests (Plan 19-01)
    // =========================================================================

    [Fact]
    public void WithSensitiveBodyPatterns_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveBodyPatterns(HarPiiPatterns.Email, HarPiiPatterns.Ssn);

        // Assert
        options.SensitiveBodyPatterns.Should().NotBeNull();
        options.SensitiveBodyPatterns.Should().HaveCount(2);
        options.SensitiveBodyPatterns.Should().Contain(HarPiiPatterns.Email);
        options.SensitiveBodyPatterns.Should().Contain(HarPiiPatterns.Ssn);
    }

    [Fact]
    public void SensitiveBodyPatterns_DefaultNull()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.SensitiveBodyPatterns.Should().BeNull();
    }

    [Fact]
    public void WithSensitiveBodyPatterns_FluentChaining_Works()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveHeaders("Authorization")
            .WithSensitiveBodyPatterns(HarPiiPatterns.CreditCard);

        // Assert
        options.SensitiveHeaders.Should().Contain("Authorization");
        options.SensitiveBodyPatterns.Should().Contain(HarPiiPatterns.CreditCard);
    }

    // =========================================================================
    // CaptureOptions.WithMaxWebSocketFramesPerConnection Tests (Plan 19-03)
    // =========================================================================

    [Fact]
    public void MaxWebSocketFramesPerConnection_Default_IsZero()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.MaxWebSocketFramesPerConnection.Should().Be(0);
    }

    [Fact]
    public void WithMaxWebSocketFramesPerConnection_SetsValue()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithMaxWebSocketFramesPerConnection(200);

        // Assert
        options.MaxWebSocketFramesPerConnection.Should().Be(200);
    }

    [Fact]
    public void WithMaxWebSocketFramesPerConnection_Returnsself_ForFluentChaining()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveBodyPatterns(HarPiiPatterns.Email)
            .WithMaxWebSocketFramesPerConnection(50);

        // Assert
        options.SensitiveBodyPatterns.Should().Contain(HarPiiPatterns.Email);
        options.MaxWebSocketFramesPerConnection.Should().Be(50);
    }

    // =========================================================================
    // HarPiiPatterns Tests (Plan 19-01)
    // =========================================================================

    [Fact]
    public void HarPiiPatterns_CreditCard_MatchesVisa()
    {
        // Arrange
        var regex = new Regex(HarPiiPatterns.CreditCard);

        // Act & Assert
        regex.IsMatch("4111111111111111").Should().BeTrue();
    }

    [Fact]
    public void HarPiiPatterns_Email_MatchesStandard()
    {
        // Arrange
        var regex = new Regex(HarPiiPatterns.Email);

        // Act & Assert
        regex.IsMatch("user@example.com").Should().BeTrue();
    }

    [Fact]
    public void HarPiiPatterns_Ssn_MatchesDashed()
    {
        // Arrange
        var regex = new Regex(HarPiiPatterns.Ssn);

        // Act & Assert
        regex.IsMatch("123-45-6789").Should().BeTrue();
    }

    [Fact]
    public void HarPiiPatterns_Phone_MatchesUS()
    {
        // Arrange
        var regex = new Regex(HarPiiPatterns.Phone);

        // Act & Assert
        regex.IsMatch("(555) 123-4567").Should().BeTrue();
    }

    [Fact]
    public void HarPiiPatterns_IpAddress_MatchesIPv4()
    {
        // Arrange
        var regex = new Regex(HarPiiPatterns.IpAddress);

        // Act & Assert
        regex.IsMatch("192.168.1.1").Should().BeTrue();
    }
}
