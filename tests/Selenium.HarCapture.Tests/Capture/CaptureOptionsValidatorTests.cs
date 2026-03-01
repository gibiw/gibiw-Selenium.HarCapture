using System;
using System.Collections.Generic;
using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Internal;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public class CaptureOptionsValidatorTests
{
    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateAndThrow_DefaultOptions_DoesNotThrow()
    {
        var options = new CaptureOptions();
        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_EnableCompressionTrue_ForceSeleniumFalse_DoesNotThrow()
    {
        var options = new CaptureOptions { EnableCompression = true, ForceSeleniumNetworkApi = false };
        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_ResponseBodyScopeNone_MaxResponseBodySizeZero_DoesNotThrow()
    {
        var options = new CaptureOptions { ResponseBodyScope = ResponseBodyScope.None, MaxResponseBodySize = 0 };
        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_ResponseBodyScopeAll_MaxResponseBodySizePositive_DoesNotThrow()
    {
        var options = new CaptureOptions { ResponseBodyScope = ResponseBodyScope.All, MaxResponseBodySize = 1024 };
        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);
        act.Should().NotThrow();
    }

    // ── Conflict: EnableCompression + ForceSeleniumNetworkApi ─────────────────

    [Fact]
    public void ValidateAndThrow_EnableCompressionAndForceSeleniumNetwork_ThrowsArgumentException()
    {
        var options = new CaptureOptions { EnableCompression = true, ForceSeleniumNetworkApi = true };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("EnableCompression")
            .And.Contain("ForceSeleniumNetworkApi");
    }

    // ── Conflict: ResponseBodyScope.None + MaxResponseBodySize > 0 ────────────

    [Fact]
    public void ValidateAndThrow_ResponseBodyScopeNone_MaxResponseBodySizePositive_ThrowsArgumentException()
    {
        var options = new CaptureOptions { ResponseBodyScope = ResponseBodyScope.None, MaxResponseBodySize = 1024 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("ResponseBodyScope")
            .And.Contain("MaxResponseBodySize");
    }

    // ── Field-level: MaxResponseBodySize < 0 ─────────────────────────────────

    [Fact]
    public void ValidateAndThrow_MaxResponseBodySizeNegative_ThrowsArgumentException()
    {
        var options = new CaptureOptions { MaxResponseBodySize = -1 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("MaxResponseBodySize");
    }

    // ── Field-level: CreatorName empty ────────────────────────────────────────

    [Fact]
    public void ValidateAndThrow_CreatorNameEmpty_ThrowsArgumentException()
    {
        var options = new CaptureOptions { CreatorName = "" };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("CreatorName");
    }

    // ── Field-level: UrlIncludePatterns with null/empty entry ─────────────────

    [Fact]
    public void ValidateAndThrow_UrlIncludePatternsContainsNullEntry_ThrowsArgumentException()
    {
        var options = new CaptureOptions
        {
            UrlIncludePatterns = new List<string> { "https://example.com/**", null! }
        };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("UrlIncludePatterns[1]");
    }

    [Fact]
    public void ValidateAndThrow_UrlIncludePatternsContainsEmptyEntry_ThrowsArgumentException()
    {
        var options = new CaptureOptions
        {
            UrlIncludePatterns = new List<string> { "" }
        };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("UrlIncludePatterns[0]");
    }

    // ── Field-level: UrlExcludePatterns with null/empty entry ─────────────────

    [Fact]
    public void ValidateAndThrow_UrlExcludePatternsContainsNullEntry_ThrowsArgumentException()
    {
        var options = new CaptureOptions
        {
            UrlExcludePatterns = new List<string> { null! }
        };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("UrlExcludePatterns[0]");
    }

    [Fact]
    public void ValidateAndThrow_UrlExcludePatternsContainsEmptyEntry_ThrowsArgumentException()
    {
        var options = new CaptureOptions
        {
            UrlExcludePatterns = new List<string> { "**/*.png", "" }
        };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("UrlExcludePatterns[1]");
    }

    // ── Multiple violations in one exception ──────────────────────────────────

    [Fact]
    public void ValidateAndThrow_MultipleViolations_SingleExceptionListsAll()
    {
        var options = new CaptureOptions
        {
            EnableCompression = true,
            ForceSeleniumNetworkApi = true,
            MaxResponseBodySize = -1,
            CreatorName = ""
        };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("EnableCompression");
        ex.Message.Should().Contain("ForceSeleniumNetworkApi");
        ex.Message.Should().Contain("MaxResponseBodySize");
        ex.Message.Should().Contain("CreatorName");
        ex.Message.Should().Contain("3 error(s)");
    }

    // ── Field-level: MaxWebSocketFramesPerConnection ──────────────────────────

    [Fact]
    public void ValidateAndThrow_MaxWebSocketFramesPerConnection_Negative_ThrowsArgumentException()
    {
        var options = new CaptureOptions { MaxWebSocketFramesPerConnection = -1 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("MaxWebSocketFramesPerConnection");
    }

    [Fact]
    public void ValidateAndThrow_MaxWebSocketFramesPerConnection_Zero_NoError()
    {
        var options = new CaptureOptions { MaxWebSocketFramesPerConnection = 0 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_MaxWebSocketFramesPerConnection_Positive_NoError()
    {
        var options = new CaptureOptions { MaxWebSocketFramesPerConnection = 100 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().NotThrow();
    }

    // ── Field-level: MaxOutputFileSize ────────────────────────────────────────

    [Fact]
    public void Validator_MaxOutputFileSize_Negative_Throws()
    {
        var options = new CaptureOptions { MaxOutputFileSize = -1 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("MaxOutputFileSize");
    }

    [Fact]
    public void Validator_MaxOutputFileSize_WithoutOutputFile_Throws()
    {
        var options = new CaptureOptions { MaxOutputFileSize = 1024, OutputFilePath = null };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("MaxOutputFileSize")
            .And.Contain("OutputFilePath");
    }

    [Fact]
    public void Validator_MaxOutputFileSize_Zero_OK()
    {
        var options = new CaptureOptions { MaxOutputFileSize = 0 };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validator_MaxOutputFileSize_WithOutputFile_OK()
    {
        var options = new CaptureOptions
        {
            MaxOutputFileSize = 1024,
            OutputFilePath = "/tmp/test.har"
        };

        Action act = () => CaptureOptionsValidator.ValidateAndThrow(options);

        act.Should().NotThrow();
    }
}
