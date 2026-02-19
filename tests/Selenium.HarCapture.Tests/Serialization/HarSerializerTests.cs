using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.Tests.Serialization;

/// <summary>
/// Tests for HarSerializer covering serialization, deserialization, file I/O, and round-trip scenarios.
/// </summary>
public sealed class HarSerializerTests
{
    [Fact]
    public void Serialize_ValidHar_ProducesValidJson()
    {
        // Arrange
        var har = CreateMinimalHar();

        // Act
        var json = HarSerializer.Serialize(har);

        // Assert
        json.Should().Contain("\"log\"");
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"creator\"");
        json.Should().Contain("\"entries\"");
    }

    [Fact]
    public void Serialize_Indented_ProducesFormattedJson()
    {
        // Arrange
        var har = CreateMinimalHar();

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: true);

        // Assert
        json.Should().Contain("\n", "indented JSON should contain newlines");
        json.Should().Contain("  ", "indented JSON should contain spaces for indentation");
    }

    [Fact]
    public void Serialize_Compact_ProducesMinifiedJson()
    {
        // Arrange
        var har = CreateMinimalHar();

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        // Should not have significant whitespace (allowing for potential whitespace in string values)
        var lines = json.Split('\n');
        lines.Should().HaveCount(1, "compact JSON should be on a single line");
    }

    [Fact]
    public void Serialize_OptionalNullFields_AreOmitted()
    {
        // Arrange
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry>
                {
                    new HarEntry
                    {
                        StartedDateTime = DateTimeOffset.UtcNow,
                        Time = 100,
                        Request = new HarRequest
                        {
                            Method = "GET",
                            Url = "https://example.com",
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
                            Content = new HarContent
                            {
                                Size = 0,
                                MimeType = "text/html"
                            },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = -1
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings
                        {
                            Send = 1,
                            Wait = 50,
                            Receive = 49
                        },
                        // Explicitly set optional fields to null
                        PageRef = null,
                        ServerIPAddress = null,
                        Connection = null,
                        Comment = null
                    }
                }
            }
        };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        json.Should().NotContain("\"pageref\"", "null pageref should be omitted");
        json.Should().NotContain("\"serverIPAddress\"", "null serverIPAddress should be omitted");
        json.Should().NotContain("\"connection\"", "null connection should be omitted");
        json.Should().NotContain("\"comment\"", "null comment should be omitted");
    }

    [Fact]
    public void Serialize_OptionalNonNullFields_AreIncluded()
    {
        // Arrange
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry>
                {
                    new HarEntry
                    {
                        PageRef = "page_1",
                        StartedDateTime = DateTimeOffset.UtcNow,
                        Time = 100,
                        Request = new HarRequest
                        {
                            Method = "GET",
                            Url = "https://example.com",
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
                            Content = new HarContent
                            {
                                Size = 0,
                                MimeType = "text/html"
                            },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = -1
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings
                        {
                            Send = 1,
                            Wait = 50,
                            Receive = 49
                        }
                    }
                }
            }
        };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        json.Should().Contain("\"pageref\"", "non-null pageref should be included");
        json.Should().Contain("\"page_1\"");
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsHarObject()
    {
        // Arrange
        var json = """
        {
            "log": {
                "version": "1.2",
                "creator": {
                    "name": "TestCreator",
                    "version": "1.0.0"
                },
                "entries": []
            }
        }
        """;

        // Act
        var har = HarSerializer.Deserialize(json);

        // Assert
        har.Should().NotBeNull();
        har.Log.Should().NotBeNull();
        har.Log.Version.Should().Be("1.2");
        har.Log.Creator.Name.Should().Be("TestCreator");
    }

    [Fact]
    public void Deserialize_SampleHarFile_LoadsCorrectly()
    {
        // Arrange
        var samplePath = Path.Combine("Serialization", "TestData", "sample.har");
        var json = File.ReadAllText(samplePath);

        // Act
        var har = HarSerializer.Deserialize(json);

        // Assert
        har.Should().NotBeNull();
        har.Log.Version.Should().Be("1.2");
        har.Log.Creator.Name.Should().Be("Selenium.HarCapture");
        har.Log.Browser.Should().NotBeNull();
        har.Log.Browser!.Name.Should().Be("Chrome");
        har.Log.Pages.Should().HaveCount(1);
        har.Log.Pages![0].Id.Should().Be("page_1");
        har.Log.Entries.Should().HaveCount(1);

        var entry = har.Log.Entries[0];
        entry.PageRef.Should().Be("page_1");
        entry.StartedDateTime.Offset.Should().Be(TimeSpan.FromHours(2));
        entry.Request.Method.Should().Be("GET");
        entry.Response.Status.Should().Be(200);
        entry.ServerIPAddress.Should().Be("93.184.216.34");
        entry.Comment.Should().Be("Sample entry for testing");
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesAllData()
    {
        // Arrange
        var original = CreateSampleHar();

        // Act
        var json = HarSerializer.Serialize(original);
        var deserialized = HarSerializer.Deserialize(json);

        // Assert
        deserialized.Should().BeEquivalentTo(original, options => options
            .Using<DateTimeOffset>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
            .WhenTypeIs<DateTimeOffset>());
    }

    [Fact]
    public void Serialize_DateTimeOffset_UsesIso8601()
    {
        // Arrange
        var specificDateTime = new DateTimeOffset(2009, 7, 24, 19, 20, 30, 450, TimeSpan.FromHours(1));
        var har = new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "Test", Version = "1.0" },
                Entries = new List<HarEntry>
                {
                    new HarEntry
                    {
                        StartedDateTime = specificDateTime,
                        Time = 100,
                        Request = new HarRequest
                        {
                            Method = "GET",
                            Url = "https://example.com",
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
                            Content = new HarContent
                            {
                                Size = 0,
                                MimeType = "text/html"
                            },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = -1
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings
                        {
                            Send = 1,
                            Wait = 50,
                            Receive = 49
                        }
                    }
                }
            }
        };

        // Act
        var json = HarSerializer.Serialize(har);

        // Assert
        // JSON may escape + as \u002B, so check for both
        json.Should().Match(j => j.Contains("2009-07-24T19:20:30.4500000+01:00") || j.Contains("2009-07-24T19:20:30.4500000\\u002B01:00"),
            "should use ISO 8601 format with timezone");
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        // Arrange
        var har = CreateMinimalHar();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await HarSerializer.SaveAsync(har, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue("file should be created");
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("\"log\"");

            // Verify it's valid JSON
            var act = () => JsonDocument.Parse(content);
            act.Should().NotThrow("saved content should be valid JSON");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ReadsFile()
    {
        // Arrange
        var original = CreateMinimalHar();
        var tempFile = Path.GetTempFileName();

        try
        {
            await HarSerializer.SaveAsync(original, tempFile);

            // Act
            var loaded = await HarSerializer.LoadAsync(tempFile);

            // Assert
            loaded.Should().NotBeNull();
            loaded.Log.Version.Should().Be("1.2");
            loaded.Log.Creator.Name.Should().Be(original.Log.Creator.Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_LoadAsync_RoundTrip()
    {
        // Arrange
        var original = CreateSampleHar();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await HarSerializer.SaveAsync(original, tempFile);
            var loaded = await HarSerializer.LoadAsync(tempFile);

            // Assert
            loaded.Should().BeEquivalentTo(original, options => options
                .Using<DateTimeOffset>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
                .WhenTypeIs<DateTimeOffset>());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_NonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".har");

        // Act
        var act = async () => await HarSerializer.LoadAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"*{nonExistentPath}*");
    }

    [Fact]
    public void Serialize_NullHar_ThrowsArgumentNullException()
    {
        // Act
        var act = () => HarSerializer.Serialize(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("har");
    }

    [Fact]
    public void Deserialize_EmptyString_ThrowsArgumentException()
    {
        // Act
        var act = () => HarSerializer.Deserialize("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("json");
    }

    // Helper methods

    private static Har CreateMinimalHar()
    {
        return new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator
                {
                    Name = "Selenium.HarCapture.Tests",
                    Version = "1.0.0"
                },
                Entries = new List<HarEntry>()
            }
        };
    }

    private static Har CreateSampleHar()
    {
        var startTime = new DateTimeOffset(2009, 7, 24, 19, 20, 30, 450, TimeSpan.FromHours(2));

        return new Har
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator
                {
                    Name = "Selenium.HarCapture",
                    Version = "1.0.0",
                    Comment = "Created by test"
                },
                Browser = new HarBrowser
                {
                    Name = "Chrome",
                    Version = "120.0.0",
                    Comment = "Test browser"
                },
                Pages = new List<HarPage>
                {
                    new HarPage
                    {
                        StartedDateTime = startTime,
                        Id = "page_1",
                        Title = "Test Page",
                        PageTimings = new HarPageTimings
                        {
                            OnContentLoad = 1500.0,
                            OnLoad = 2500.0,
                            Comment = "Page timings"
                        },
                        Comment = "Test page"
                    }
                },
                Entries = new List<HarEntry>
                {
                    new HarEntry
                    {
                        PageRef = "page_1",
                        StartedDateTime = startTime,
                        Time = 81.0,
                        Request = new HarRequest
                        {
                            Method = "GET",
                            Url = "https://example.com/index.html",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>
                            {
                                new HarCookie
                                {
                                    Name = "test_cookie",
                                    Value = "test_value",
                                    Path = "/",
                                    Domain = "example.com",
                                    Expires = startTime.AddDays(1),
                                    HttpOnly = true,
                                    Secure = true,
                                    Comment = "Test cookie"
                                }
                            },
                            Headers = new List<HarHeader>
                            {
                                new HarHeader
                                {
                                    Name = "User-Agent",
                                    Value = "Mozilla/5.0",
                                    Comment = "Test header"
                                }
                            },
                            QueryString = new List<HarQueryString>
                            {
                                new HarQueryString
                                {
                                    Name = "q",
                                    Value = "test",
                                    Comment = "Test query"
                                }
                            },
                            PostData = new HarPostData
                            {
                                MimeType = "application/x-www-form-urlencoded",
                                Params = new List<HarParam>
                                {
                                    new HarParam
                                    {
                                        Name = "field1",
                                        Value = "value1",
                                        FileName = null,
                                        ContentType = null,
                                        Comment = "Test param"
                                    }
                                },
                                Text = "field1=value1",
                                Comment = "Test post data"
                            },
                            HeadersSize = 200,
                            BodySize = 13,
                            Comment = "Test request"
                        },
                        Response = new HarResponse
                        {
                            Status = 200,
                            StatusText = "OK",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>
                            {
                                new HarHeader
                                {
                                    Name = "Content-Type",
                                    Value = "text/html",
                                    Comment = "Response header"
                                }
                            },
                            Content = new HarContent
                            {
                                Size = 1024,
                                Compression = 0,
                                MimeType = "text/html",
                                Text = "<!DOCTYPE html><html><body>Test</body></html>",
                                Encoding = "utf-8",
                                Comment = "Test content"
                            },
                            RedirectURL = "",
                            HeadersSize = 150,
                            BodySize = 1024,
                            Comment = "Test response"
                        },
                        Cache = new HarCache
                        {
                            BeforeRequest = new HarCacheEntry
                            {
                                Expires = startTime.AddHours(1),
                                LastAccess = startTime.AddMinutes(-10),
                                ETag = "\"abc123\"",
                                HitCount = 5,
                                Comment = "Before cache"
                            },
                            AfterRequest = new HarCacheEntry
                            {
                                Expires = startTime.AddHours(2),
                                LastAccess = startTime,
                                ETag = "\"abc123\"",
                                HitCount = 6,
                                Comment = "After cache"
                            },
                            Comment = "Test cache"
                        },
                        Timings = new HarTimings
                        {
                            Blocked = -1.0,
                            Dns = 5.0,
                            Connect = 10.0,
                            Send = 1.0,
                            Wait = 50.0,
                            Receive = 15.0,
                            Ssl = 8.0,
                            Comment = "Test timings"
                        },
                        ServerIPAddress = "93.184.216.34",
                        Connection = "12345",
                        Comment = "Test entry"
                    }
                },
                Comment = "Test log"
            }
        };
    }
}
