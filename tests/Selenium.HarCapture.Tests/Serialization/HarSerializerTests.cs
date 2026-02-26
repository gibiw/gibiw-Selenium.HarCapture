using System.IO.Compression;
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

    [Fact]
    public void Save_WritesCompleteValidJson()
    {
        // Arrange
        var har = CreateHarWithMultipleEntries(5);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            HarSerializer.Save(har, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue("file should be created");
            var content = File.ReadAllText(tempFile);
            content.Should().Contain("\"log\"");

            // Verify it's valid JSON by deserializing back
            var loaded = HarSerializer.Deserialize(content);
            loaded.Should().NotBeNull();
            loaded.Log.Entries.Should().HaveCount(5, "all entries should be present");

            // Verify file size
            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().BeGreaterThan(0, "file should not be empty");
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
    public void Save_LargePayload_WritesWithoutTruncation()
    {
        // Arrange - Create HAR with large response bodies (>1.5MB total)
        var har = CreateHarWithLargePayload(3, 500_000); // 3 entries, 500KB each
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            HarSerializer.Save(har, tempFile);

            // Assert
            var expectedJson = HarSerializer.Serialize(har);
            var expectedLength = System.Text.Encoding.UTF8.GetByteCount(expectedJson);

            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().Be(expectedLength, "file size should match in-memory serialization length (no truncation)");

            // Verify deserialization works
            var loaded = HarSerializer.Load(tempFile);
            loaded.Should().NotBeNull();
            loaded.Log.Entries.Should().HaveCount(3, "all entries should be present");

            // Verify body lengths are preserved
            for (int i = 0; i < 3; i++)
            {
                var originalBodyLength = har.Log.Entries[i].Response.Content.Text?.Length ?? 0;
                var loadedBodyLength = loaded.Log.Entries[i].Response.Content.Text?.Length ?? 0;
                loadedBodyLength.Should().Be(originalBodyLength, $"entry {i} body length should match");
            }
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
    public async Task SaveAsync_CalledSynchronously_WritesCompleteFile()
    {
        // Arrange - Verify SaveAsync writes complete file
        var har = CreateHarWithLargePayload(3, 400_000); // >1MB total
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await HarSerializer.SaveAsync(har, tempFile);

            // Assert
            var expectedJson = HarSerializer.Serialize(har);
            var expectedLength = System.Text.Encoding.UTF8.GetByteCount(expectedJson);

            File.Exists(tempFile).Should().BeTrue("file should be created");

            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().Be(expectedLength, "file size should match in-memory serialization length (no truncation)");

            // Verify it's valid JSON
            var loaded = HarSerializer.Load(tempFile);
            loaded.Should().NotBeNull();
            loaded.Log.Entries.Should().HaveCount(3, "all entries should be present");
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
    public void Load_ValidFile_ReturnsHar()
    {
        // Arrange
        var original = CreateSampleHar();
        var tempFile = Path.GetTempFileName();

        try
        {
            HarSerializer.Save(original, tempFile);

            // Act
            var loaded = HarSerializer.Load(tempFile);

            // Assert
            loaded.Should().NotBeNull();
            loaded.Log.Version.Should().Be(original.Log.Version);
            loaded.Log.Creator.Name.Should().Be(original.Log.Creator.Name);
            loaded.Log.Entries.Should().HaveCount(original.Log.Entries.Count);

            // Verify key fields from first entry
            var originalEntry = original.Log.Entries[0];
            var loadedEntry = loaded.Log.Entries[0];
            loadedEntry.Request.Method.Should().Be(originalEntry.Request.Method);
            loadedEntry.Request.Url.Should().Be(originalEntry.Request.Url);
            loadedEntry.Response.Status.Should().Be(originalEntry.Response.Status);
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
    public void Save_NullHar_ThrowsArgumentNullException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var act = () => HarSerializer.Save(null!, tempFile);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("har");
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
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".har");

        // Act
        var act = () => HarSerializer.Load(nonExistentPath);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage($"*{nonExistentPath}*");
    }

    // Compression tests

    [Fact]
    public async Task SaveAsync_GzExtension_CompressesFile()
    {
        // Arrange
        var har = CreateMinimalHar();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har.gz");

        try
        {
            // Act
            await HarSerializer.SaveAsync(har, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue("file should be created");
            var fileBytes = File.ReadAllBytes(tempFile);
            fileBytes.Length.Should().BeGreaterThan(2, "file should contain data");
            fileBytes[0].Should().Be(0x1F, "first byte should be gzip magic number");
            fileBytes[1].Should().Be(0x8B, "second byte should be gzip magic number");
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
    public void Save_GzExtension_CompressesFile()
    {
        // Arrange
        var har = CreateMinimalHar();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har.gz");

        try
        {
            // Act
            HarSerializer.Save(har, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue("file should be created");
            var fileBytes = File.ReadAllBytes(tempFile);
            fileBytes.Length.Should().BeGreaterThan(2, "file should contain data");
            fileBytes[0].Should().Be(0x1F, "first byte should be gzip magic number");
            fileBytes[1].Should().Be(0x8B, "second byte should be gzip magic number");
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
    public async Task LoadAsync_GzExtension_DecompressesFile()
    {
        // Arrange
        var original = CreateMinimalHar();
        var gzPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har.gz");

        try
        {
            // Manually create compressed file
            var json = HarSerializer.Serialize(original);
            using (var fileStream = new FileStream(gzPath, FileMode.Create))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                gzipStream.Write(bytes, 0, bytes.Length);
            }

            // Act
            var loaded = await HarSerializer.LoadAsync(gzPath);

            // Assert
            loaded.Should().NotBeNull();
            loaded.Log.Version.Should().Be(original.Log.Version);
            loaded.Log.Creator.Name.Should().Be(original.Log.Creator.Name);
        }
        finally
        {
            if (File.Exists(gzPath))
            {
                File.Delete(gzPath);
            }
        }
    }

    [Fact]
    public void Load_GzExtension_DecompressesFile()
    {
        // Arrange
        var original = CreateMinimalHar();
        var gzPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har.gz");

        try
        {
            // Manually create compressed file
            var json = HarSerializer.Serialize(original);
            using (var fileStream = new FileStream(gzPath, FileMode.Create))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                gzipStream.Write(bytes, 0, bytes.Length);
            }

            // Act
            var loaded = HarSerializer.Load(gzPath);

            // Assert
            loaded.Should().NotBeNull();
            loaded.Log.Version.Should().Be(original.Log.Version);
            loaded.Log.Creator.Name.Should().Be(original.Log.Creator.Name);
        }
        finally
        {
            if (File.Exists(gzPath))
            {
                File.Delete(gzPath);
            }
        }
    }

    [Fact]
    public async Task SaveLoad_Compressed_RoundTrip_PreservesData()
    {
        // Arrange
        var original = CreateSampleHar();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har.gz");

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
    public void Compressed_File_IsSmallerThanOriginal()
    {
        // Arrange
        var har = CreateHarWithLargePayload(3, 100_000);
        var harPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har");
        var gzPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har.gz");

        try
        {
            // Act
            HarSerializer.Save(har, harPath);
            HarSerializer.Save(har, gzPath);

            // Assert
            var harFileInfo = new FileInfo(harPath);
            var gzFileInfo = new FileInfo(gzPath);

            gzFileInfo.Length.Should().BeLessThan(harFileInfo.Length,
                "compressed .gz file should be smaller than uncompressed .har file");
        }
        finally
        {
            if (File.Exists(harPath))
            {
                File.Delete(harPath);
            }
            if (File.Exists(gzPath))
            {
                File.Delete(gzPath);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var har = CreateMinimalHar();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "sub", "dir");
        var filePath = Path.Combine(tempDir, "test.har");

        try
        {
            // Act
            await HarSerializer.SaveAsync(har, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue("file should be created in auto-created directory");
        }
        finally
        {
            // Clean up the top-level temp directory
            var root = Path.Combine(Path.GetTempPath(), Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)!)!)!));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var har = CreateMinimalHar();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "sub", "dir");
        var filePath = Path.Combine(tempDir, "test.har");

        try
        {
            // Act
            HarSerializer.Save(har, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue("file should be created in auto-created directory");
        }
        finally
        {
            var root = Path.Combine(Path.GetTempPath(), Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)!)!)!));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // WebSocket serialization tests

    [Fact]
    public void Serialize_EntryWithNullWebSocketMessages_OmitsField()
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
                            Content = new HarContent { Size = 0, MimeType = "text/html" },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = -1
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 },
                        WebSocketMessages = null
                    }
                }
            }
        };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        json.Should().NotContain("_webSocketMessages",
            "null WebSocketMessages should be omitted from JSON");
    }

    [Fact]
    public void Serialize_Deserialize_WebSocketMessages_RoundTrip()
    {
        // Arrange
        var wsMessages = new List<HarWebSocketMessage>
        {
            new HarWebSocketMessage { Type = "send", Time = 1700000001.0, Opcode = 1, Data = "hello" },
            new HarWebSocketMessage { Type = "receive", Time = 1700000002.0, Opcode = 1, Data = "world" },
            new HarWebSocketMessage { Type = "send", Time = 1700000003.0, Opcode = 2, Data = "binary" }
        };

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
                            Url = "wss://example.com/socket",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>(),
                            QueryString = new List<HarQueryString>(),
                            HeadersSize = -1,
                            BodySize = 0
                        },
                        Response = new HarResponse
                        {
                            Status = 101,
                            StatusText = "Switching Protocols",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>(),
                            Content = new HarContent { Size = 0, MimeType = "x-unknown" },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = 0
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings { Send = 0, Wait = 0, Receive = 0 },
                        WebSocketMessages = wsMessages
                    }
                }
            }
        };

        // Act
        var json = HarSerializer.Serialize(har);
        var deserialized = HarSerializer.Deserialize(json);

        // Assert
        deserialized.Log.Entries[0].WebSocketMessages.Should().NotBeNull();
        deserialized.Log.Entries[0].WebSocketMessages.Should().HaveCount(3);

        var msgs = deserialized.Log.Entries[0].WebSocketMessages!;
        msgs[0].Type.Should().Be("send");
        msgs[0].Data.Should().Be("hello");
        msgs[0].Opcode.Should().Be(1);
        msgs[1].Type.Should().Be("receive");
        msgs[2].Opcode.Should().Be(2);
    }

    [Fact]
    public void Serialize_WebSocketMessages_UsesCorrectFieldName()
    {
        // Arrange — verify Chrome DevTools format compatibility
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
                        Time = 0,
                        Request = new HarRequest
                        {
                            Method = "GET",
                            Url = "wss://example.com",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>(),
                            QueryString = new List<HarQueryString>(),
                            HeadersSize = -1,
                            BodySize = 0
                        },
                        Response = new HarResponse
                        {
                            Status = 101,
                            StatusText = "Switching Protocols",
                            HttpVersion = "HTTP/1.1",
                            Cookies = new List<HarCookie>(),
                            Headers = new List<HarHeader>(),
                            Content = new HarContent { Size = 0, MimeType = "x-unknown" },
                            RedirectURL = "",
                            HeadersSize = -1,
                            BodySize = 0
                        },
                        Cache = new HarCache(),
                        Timings = new HarTimings { Send = 0, Wait = 0, Receive = 0 },
                        WebSocketMessages = new List<HarWebSocketMessage>
                        {
                            new HarWebSocketMessage { Type = "send", Time = 1.0, Opcode = 1, Data = "test" }
                        }
                    }
                }
            }
        };

        // Act
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert — Chrome DevTools uses _webSocketMessages with "type" values "send"/"receive"
        json.Should().Contain("\"_webSocketMessages\"",
            "field name should match Chrome DevTools format");
        json.Should().Contain("\"type\":\"send\"",
            "type value should be 'send' or 'receive' per Chrome format");
        json.Should().Contain("\"opcode\":1",
            "opcode should be present per Chrome format");
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

    private static Har CreateHarWithMultipleEntries(int entryCount)
    {
        var startTime = DateTimeOffset.UtcNow;
        var entries = new List<HarEntry>();

        for (int i = 0; i < entryCount; i++)
        {
            entries.Add(new HarEntry
            {
                StartedDateTime = startTime.AddSeconds(i),
                Time = 100,
                Request = new HarRequest
                {
                    Method = "GET",
                    Url = $"https://example.com/resource{i}",
                    HttpVersion = "HTTP/1.1",
                    Cookies = new List<HarCookie>(),
                    Headers = new List<HarHeader>
                    {
                        new HarHeader { Name = "User-Agent", Value = "Test" }
                    },
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
                    Headers = new List<HarHeader>
                    {
                        new HarHeader { Name = "Content-Type", Value = "text/html" }
                    },
                    Content = new HarContent
                    {
                        Size = 100,
                        MimeType = "text/html",
                        Text = $"<html><body>Content {i}</body></html>"
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
            });
        }

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
                Entries = entries
            }
        };
    }

    private static Har CreateHarWithLargePayload(int entryCount, int bodySizePerEntry)
    {
        var startTime = DateTimeOffset.UtcNow;
        var entries = new List<HarEntry>();

        for (int i = 0; i < entryCount; i++)
        {
            var largeBody = new string('X', bodySizePerEntry);

            entries.Add(new HarEntry
            {
                StartedDateTime = startTime.AddSeconds(i),
                Time = 100,
                Request = new HarRequest
                {
                    Method = "GET",
                    Url = $"https://example.com/large-resource{i}",
                    HttpVersion = "HTTP/1.1",
                    Cookies = new List<HarCookie>(),
                    Headers = new List<HarHeader>
                    {
                        new HarHeader { Name = "User-Agent", Value = "Test" }
                    },
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
                    Headers = new List<HarHeader>
                    {
                        new HarHeader { Name = "Content-Type", Value = "application/json" }
                    },
                    Content = new HarContent
                    {
                        Size = bodySizePerEntry,
                        MimeType = "application/json",
                        Text = largeBody
                    },
                    RedirectURL = "",
                    HeadersSize = -1,
                    BodySize = bodySizePerEntry
                },
                Cache = new HarCache(),
                Timings = new HarTimings
                {
                    Send = 1,
                    Wait = 50,
                    Receive = 49
                }
            });
        }

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
                Entries = entries
            }
        };
    }
}
