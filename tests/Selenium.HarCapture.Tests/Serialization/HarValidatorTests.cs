using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.Tests.Serialization;

/// <summary>
/// Tests for <see cref="HarValidator"/> covering golden file acceptance, null-field detection,
/// mode-specific behaviour, value range checks, and multiple-error collection.
/// </summary>
public sealed class HarValidatorTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string GoldenPath(string name) =>
        Path.Combine("Serialization", "TestData", name);

    private static Har LoadGolden(string name) =>
        HarSerializer.Load(GoldenPath(name));

    private static Har MinimalHar(Action<HarLog>? configureLog = null)
    {
        var log = new HarLog
        {
            Version = "1.2",
            Creator = new HarCreator { Name = "Test", Version = "1.0" },
            Entries = new List<HarEntry>()
        };
        configureLog?.Invoke(log);
        return new Har { Log = log };
    }

    private static HarEntry MinimalEntry() => new HarEntry
    {
        StartedDateTime = DateTimeOffset.UtcNow,
        Time = 10.0,
        Request = new HarRequest
        {
            Method = "GET",
            Url = "https://example.com/",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            QueryString = new List<HarQueryString>(),
            HeadersSize = -1,
            BodySize = 0
        },
        Response = new HarResponse
        {
            Status = 200,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = new HarContent { Size = 0, MimeType = "text/html" },
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        },
        Cache = new HarCache(),
        Timings = new HarTimings { Send = 1.0, Wait = 5.0, Receive = 4.0 }
    };

    // ── Golden file acceptance (Standard mode) ───────────────────────────────────

    [Fact]
    public void GoldenCdpAllBodies_StandardMode_IsValid()
    {
        var har = LoadGolden("golden-cdp-all-bodies.har");
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeTrue(because: "CDP all-bodies HAR is library-produced output");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GoldenINetworkNoBodies_StandardMode_IsValid()
    {
        var har = LoadGolden("golden-inetwork-no-bodies.har");
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeTrue(because: "INetwork no-bodies HAR uses -1 sentinels which Standard mode accepts");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GoldenCdpWebSocket_StandardMode_IsValid()
    {
        var har = LoadGolden("golden-cdp-websocket.har");
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeTrue(because: "WebSocket HAR uses _resourceType and _webSocketMessages extensions");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GoldenCdpNoPages_StandardMode_IsValid()
    {
        var har = LoadGolden("golden-cdp-no-pages.har");
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeTrue(because: "absent pages array is valid in Standard mode");
        result.Errors.Should().BeEmpty();
    }

    // ── Null argument ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NullHar_ThrowsArgumentNullException()
    {
        var act = () => HarValidator.Validate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Null/missing field detection ─────────────────────────────────────────────

    [Fact]
    public void NullLog_ProducesErrorOnLog()
    {
        var har = new Har { Log = null! };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Field == "log" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void EmptyLogVersion_ProducesErrorOnLogVersion()
    {
        var har = MinimalHar(log => log = new HarLog
        {
            Version = "",
            Creator = new HarCreator { Name = "Test", Version = "1.0" },
            Entries = new List<HarEntry>()
        });
        // Build via direct property since init-only requires reconstruction
        var har2 = new Har
        {
            Log = new HarLog
            {
                Version = "",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry>()
            }
        };
        var result = HarValidator.Validate(har2);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.version" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullLogCreator_ProducesErrorOnLogCreator()
    {
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = null!,
                Entries = new List<HarEntry>()
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.creator" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullCreatorName_ProducesErrorOnLogCreatorName()
    {
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = null!, Version = "1.0" },
                Entries = new List<HarEntry>()
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.creator.name" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullEntries_ProducesErrorOnLogEntries()
    {
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = null!
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullEntryRequest_ProducesErrorOnEntryRequest()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = null!,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].request" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullEntryResponse_ProducesErrorOnEntryResponse()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = null!,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].response" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullEntryTimings_ProducesErrorOnEntryTimings()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = null!
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].timings" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullEntryCache_ProducesErrorOnEntryCache()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = null!,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].cache" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void EmptyRequestMethod_ProducesErrorOnRequestMethod()
    {
        var entry = MinimalEntry();
        var modifiedRequest = new HarRequest
        {
            Method = "",
            Url = "https://example.com/",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            QueryString = new List<HarQueryString>(),
            HeadersSize = -1,
            BodySize = 0
        };
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = modifiedRequest,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].request.method" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void EmptyRequestUrl_ProducesErrorOnRequestUrl()
    {
        var entry = MinimalEntry();
        var modifiedRequest = new HarRequest
        {
            Method = "GET",
            Url = "",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            QueryString = new List<HarQueryString>(),
            HeadersSize = -1,
            BodySize = 0
        };
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = modifiedRequest,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].request.url" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void NullResponseContent_ProducesErrorOnResponseContent()
    {
        var entry = MinimalEntry();
        var modifiedResponse = new HarResponse
        {
            Status = 200,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = null!,
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = modifiedResponse,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].response.content" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void EmptyContentMimeType_ProducesErrorOnContentMimeType()
    {
        var entry = MinimalEntry();
        var modifiedResponse = new HarResponse
        {
            Status = 200,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = new HarContent { Size = 0, MimeType = "" },
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = modifiedResponse,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].response.content.mimeType" && e.Severity == HarValidationSeverity.Error);
    }

    // ── Mode-specific behaviour ──────────────────────────────────────────────────

    [Fact]
    public void StrictMode_BlockedMinus1_ProducesError()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = new HarTimings { Blocked = -1, Send = 1.0, Wait = 5.0, Receive = 4.0 }
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har, HarValidationMode.Strict);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].timings.blocked" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void StandardMode_BlockedMinus1_NoError()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = new HarTimings { Blocked = -1, Send = 1.0, Wait = 5.0, Receive = 4.0 }
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har, HarValidationMode.Standard);
        result.Errors.Should().NotContain(e => e.Field == "log.entries[0].timings.blocked");
    }

    [Fact]
    public void StrictMode_AbsentPages_ProducesWarning()
    {
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Pages = null,
                Entries = new List<HarEntry>()
            }
        };
        var result = HarValidator.Validate(har, HarValidationMode.Strict);
        result.IsValid.Should().BeTrue(because: "absent pages is a Warning only, not an Error");
        result.Warnings.Should().Contain(e =>
            e.Field == "log.pages" && e.Severity == HarValidationSeverity.Warning);
    }

    [Fact]
    public void StandardMode_AbsentPages_NoErrorOrWarning()
    {
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Pages = null,
                Entries = new List<HarEntry>()
            }
        };
        var result = HarValidator.Validate(har, HarValidationMode.Standard);
        result.Errors.Should().NotContain(e => e.Field == "log.pages");
    }

    [Fact]
    public void StrictMode_RequestHeadersSizeMinus1_ProducesError()
    {
        var entry = MinimalEntry(); // already uses -1 for headersSize
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { entry }
            }
        };
        var result = HarValidator.Validate(har, HarValidationMode.Strict);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].request.headersSize" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void StandardMode_RequestHeadersSizeMinus1_Accepted()
    {
        var entry = MinimalEntry();
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { entry }
            }
        };
        var result = HarValidator.Validate(har, HarValidationMode.Standard);
        result.Errors.Should().NotContain(e => e.Field == "log.entries[0].request.headersSize");
    }

    // ── Value range checks ───────────────────────────────────────────────────────

    [Fact]
    public void ResponseStatusNegative_ProducesError()
    {
        var entry = MinimalEntry();
        var modifiedResponse = new HarResponse
        {
            Status = -1,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = new HarContent { Size = 0, MimeType = "text/html" },
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = modifiedResponse,
            Cache = entry.Cache,
            Timings = entry.Timings
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].response.status" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void TimingsSendLessThanMinus1_ProducesError()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = new HarTimings { Send = -2.0, Wait = 5.0, Receive = 4.0 }
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].timings.send" && e.Severity == HarValidationSeverity.Error);
    }

    [Fact]
    public void TimingsWaitLessThanMinus1_ProducesError()
    {
        var entry = MinimalEntry();
        var modifiedEntry = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = new HarTimings { Send = 1.0, Wait = -5.0, Receive = 4.0 }
        };
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry> { modifiedEntry }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Field == "log.entries[0].timings.wait" && e.Severity == HarValidationSeverity.Error);
    }

    // ── Multiple errors collected ─────────────────────────────────────────────────

    [Fact]
    public void MultipleViolations_AllErrorsCollected()
    {
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "",
                Creator = new HarCreator { Name = null!, Version = "1.0" },
                Entries = new List<HarEntry>
                {
                    new HarEntry
                    {
                        StartedDateTime = DateTimeOffset.UtcNow,
                        Time = 10.0,
                        Request = new HarRequest
                        {
                            Method = "",
                            Url = "",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>(),
                            QueryString = new List<HarQueryString>(),
                            HeadersSize = -1,
                            BodySize = 0
                        },
                        Response = new HarResponse
                        {
                            Status = 200,
                            StatusText = "OK",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>(),
                            Content = new HarContent { Size = 0, MimeType = "text/html" },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = -1
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings { Send = 1.0, Wait = 5.0, Receive = 4.0 }
                    }
                }
            }
        };
        var result = HarValidator.Validate(har);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }
}
