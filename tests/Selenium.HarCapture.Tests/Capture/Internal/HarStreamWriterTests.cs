using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class HarStreamWriterTests : IDisposable, IAsyncLifetime
{
    private readonly string _tempDir;

    public HarStreamWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"HarStreamWriterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private string TempFile() => Path.Combine(_tempDir, $"{Guid.NewGuid():N}.har");

    private static HarCreator DefaultCreator => new HarCreator { Name = "Test", Version = "1.0" };

    private static HarEntry CreateEntry(string url = "https://example.com", int status = 200)
    {
        return new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 100,
            Request = new HarRequest
            {
                Method = "GET",
                Url = url,
                HttpVersion = "HTTP/1.1",
                HeadersSize = -1,
                BodySize = 0,
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                Cookies = new List<HarCookie>()
            },
            Response = new HarResponse
            {
                Status = status,
                StatusText = "OK",
                HttpVersion = "HTTP/1.1",
                HeadersSize = -1,
                BodySize = 0,
                Headers = new List<HarHeader>(),
                Cookies = new List<HarCookie>(),
                Content = new HarContent { Size = 0, MimeType = "text/html" }
            },
            Cache = new HarCache(),
            Timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 }
        };
    }

    [Fact]
    public void Constructor_CreatesValidEmptyHar()
    {
        var path = TempFile();

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            writer.Count.Should().Be(0);
        }

        var har = HarSerializer.Load(path);
        har.Log.Version.Should().Be("1.2");
        har.Log.Creator.Name.Should().Be("Test");
        har.Log.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteEntry_SingleEntry_ProducesValidHar()
    {
        var path = TempFile();
        var entry = CreateEntry("https://example.com/api");

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            writer.WriteEntry(entry);
            await writer.WaitForConsumerAsync();
            writer.Count.Should().Be(1);
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(1);
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/api");
    }

    [Fact]
    public async Task WriteEntry_MultipleEntries_ProducesValidHar()
    {
        var path = TempFile();

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            for (int i = 0; i < 10; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/{i}", 200 + i));
            }

            await writer.WaitForConsumerAsync();
            writer.Count.Should().Be(10);
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(10);
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/0");
        har.Log.Entries[9].Request.Url.Should().Be("https://example.com/9");
        har.Log.Entries[9].Response.Status.Should().Be(209);
    }

    [Fact]
    public async Task AlwaysValid_FileIsValidAfterEachEntry_WithoutComplete()
    {
        var path = TempFile();

        // Write 5 entries without calling Complete or Dispose
        var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 65536);
        var writer = new HarStreamWriter(path, "1.2", DefaultCreator);

        for (int i = 1; i <= 5; i++)
        {
            writer.WriteEntry(CreateEntry($"https://example.com/{i}"));
            await writer.WaitForConsumerAsync();

            // Read the file while writer still holds it (via FileShare.Read on writer's stream)
            var content = File.ReadAllText(path);
            var har = HarSerializer.Deserialize(content);
            har.Log.Entries.Should().HaveCount(i, $"after writing {i} entries the file should be valid with {i} entries");
        }

        await writer.DisposeAsync();
    }

    [Fact]
    public void AddPage_PagesAppearInFooter()
    {
        var path = TempFile();

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            writer.WriteEntry(CreateEntry("https://example.com/page1"));

            writer.AddPage(new HarPage
            {
                Id = "page1",
                Title = "Home",
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            });

            writer.WriteEntry(CreateEntry("https://example.com/page2"));

            writer.AddPage(new HarPage
            {
                Id = "page2",
                Title = "About",
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            });
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(2);
        har.Log.Pages.Should().HaveCount(2);
        har.Log.Pages![0].Id.Should().Be("page1");
        har.Log.Pages[1].Id.Should().Be("page2");
    }

    [Fact]
    public void InitialPages_IncludedInOutput()
    {
        var path = TempFile();
        var initialPages = new List<HarPage>
        {
            new HarPage
            {
                Id = "init",
                Title = "Initial",
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            }
        };

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, initialPages: initialPages))
        {
            writer.WriteEntry(CreateEntry());
        }

        var har = HarSerializer.Load(path);
        har.Log.Pages.Should().HaveCount(1);
        har.Log.Pages![0].Id.Should().Be("init");
        har.Log.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void BrowserAndComment_IncludedInOutput()
    {
        var path = TempFile();
        var browser = new HarBrowser { Name = "Chrome", Version = "120.0" };

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, browser: browser, comment: "test comment"))
        {
            writer.WriteEntry(CreateEntry());
        }

        var har = HarSerializer.Load(path);
        har.Log.Browser.Should().NotBeNull();
        har.Log.Browser!.Name.Should().Be("Chrome");
        har.Log.Comment.Should().Be("test comment");
    }

    [Fact]
    public async Task ConcurrentWriteEntry_AllEntriesPresent()
    {
        var path = TempFile();
        const int threadCount = 8;
        const int entriesPerThread = 50;

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                var threadId = t;
                threads[t] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < entriesPerThread; i++)
                    {
                        writer.WriteEntry(CreateEntry($"https://example.com/t{threadId}/e{i}"));
                    }
                });
                threads[t].Start();
            }

            foreach (var thread in threads)
                thread.Join();

            await writer.WaitForConsumerAsync();
            writer.Count.Should().Be(threadCount * entriesPerThread);
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(threadCount * entriesPerThread);
    }

    [Fact]
    public void Dispose_WithoutComplete_StillProducesValidHar()
    {
        var path = TempFile();

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            writer.WriteEntry(CreateEntry("https://example.com/1"));
            writer.WriteEntry(CreateEntry("https://example.com/2"));
            // No Complete() call — Dispose should call it
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void Complete_CalledMultipleTimes_DoesNotThrow()
    {
        var path = TempFile();

        using var writer = new HarStreamWriter(path, "1.2", DefaultCreator);
        writer.WriteEntry(CreateEntry());
        writer.Complete();
        writer.Complete(); // should not throw
    }

    [Fact]
    public void WriteEntry_AfterDispose_ThrowsObjectDisposedException()
    {
        var path = TempFile();
        var writer = new HarStreamWriter(path, "1.2", DefaultCreator);
        writer.Dispose();

        var act = () => writer.WriteEntry(CreateEntry());
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AddPage_AfterNewEntries_FooterShrinks_TruncatesCorrectly()
    {
        // Scenario: write entry, add page with long title, write entry, add page with short title
        // The footer may shrink, testing that SetLength truncates correctly
        var path = TempFile();

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            writer.AddPage(new HarPage
            {
                Id = "page_with_a_very_long_identifier_to_make_footer_large",
                Title = "This is a very long page title that should make the footer quite large in bytes",
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            });

            writer.WriteEntry(CreateEntry());

            writer.AddPage(new HarPage
            {
                Id = "p2",
                Title = "Short",
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            });
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(1);
        har.Log.Pages.Should().HaveCount(2);
    }

    // ========== New Async Tests (Phase 10) ==========

    [Fact]
    public async Task DisposeAsync_DrainsRemainingEntries()
    {
        var path = TempFile();

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            for (int i = 0; i < 100; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/{i}"));
            }
            // DisposeAsync should drain all posted entries
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(100, "all entries should be drained on async disposal");
    }

    [Fact]
    public async Task DisposeAsync_HighTraffic_NoEntryLoss()
    {
        var path = TempFile();
        const int threadCount = 10;
        const int entriesPerThread = 100;
        const int totalExpected = threadCount * entriesPerThread;

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            var tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                var threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < entriesPerThread; i++)
                    {
                        writer.WriteEntry(CreateEntry($"https://example.com/t{threadId}/e{i}"));
                    }
                });
            }

            await Task.WhenAll(tasks);
            // DisposeAsync should drain all entries from channel
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(totalExpected, "no entries should be lost under high traffic");
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var path = TempFile();
        var writer = new HarStreamWriter(path, "1.2", DefaultCreator);

        writer.WriteEntry(CreateEntry());

        await writer.DisposeAsync();
        await writer.DisposeAsync(); // Should be idempotent

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task WriteEntry_AfterDisposeAsync_ThrowsObjectDisposedException()
    {
        var path = TempFile();
        var writer = new HarStreamWriter(path, "1.2", DefaultCreator);

        await writer.DisposeAsync();

        var act = () => writer.WriteEntry(CreateEntry());
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_Synchronous_CompletesWithoutDeadlock()
    {
        var path = TempFile();

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            for (int i = 0; i < 50; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/{i}"));
            }
            // Sync Dispose should complete without deadlock (best effort drain)
        }

        // File should exist and be valid, though some entries might be lost if drain timeout
        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().NotBeEmpty("at least some entries should be written");
    }

    // ========== MaxOutputFileSize Tests (Phase 20-02) ==========

    [Fact]
    public void HarStreamWriter_IsTruncated_FalseByDefault()
    {
        var path = TempFile();

        using var writer = new HarStreamWriter(path, "1.2", DefaultCreator);

        writer.IsTruncated.Should().BeFalse("a new writer should not be truncated");
    }

    [Fact]
    public async Task HarStreamWriter_MaxOutputFileSize_Zero_Unlimited()
    {
        var path = TempFile();

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, maxOutputFileSize: 0))
        {
            for (int i = 0; i < 20; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/{i}"));
            }
            await writer.WaitForConsumerAsync();
            writer.IsTruncated.Should().BeFalse("maxOutputFileSize=0 means unlimited");
            writer.Count.Should().Be(20);
        }
    }

    [Fact]
    public async Task HarStreamWriter_MaxOutputFileSize_TruncatesAfterExceeding()
    {
        var path = TempFile();
        // 500 bytes is less than a few entries — first couple of entries should push past the limit
        const long maxSize = 500;

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, maxOutputFileSize: maxSize))
        {
            for (int i = 0; i < 50; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/long-url-entry/{i}"));
            }
            await writer.WaitForConsumerAsync();
            writer.IsTruncated.Should().BeTrue("file should have been truncated after exceeding 500 bytes");
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().NotBeEmpty("at least one entry should have been written before truncation");
        har.Log.Entries.Count.Should().BeLessThan(50, "not all entries should fit within 500 bytes");
    }

    [Fact]
    public async Task HarStreamWriter_Truncated_FileIsValidJson()
    {
        var path = TempFile();
        // Use very small limit to force truncation after 1 entry
        const long maxSize = 600;

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, maxOutputFileSize: maxSize))
        {
            for (int i = 0; i < 30; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/test/{i}"));
            }
            await writer.WaitForConsumerAsync();
            writer.IsTruncated.Should().BeTrue();
        }

        // File must be parseable as valid JSON even after truncation
        var act = () => HarSerializer.Load(path);
        act.Should().NotThrow("truncated file must still be valid JSON");
        var har = HarSerializer.Load(path);
        har.Log.Should().NotBeNull();
        har.Log.Entries.Should().NotBeNull();
    }

    [Fact]
    public async Task HarStreamWriter_Custom_AppearsInFooter()
    {
        var path = TempFile();
        var custom = new Dictionary<string, object> { ["env"] = "test", ["txId"] = "abc" };

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, custom: custom))
        {
            writer.WriteEntry(CreateEntry("https://example.com/api"));
            await writer.WaitForConsumerAsync();
        }

        var content = System.IO.File.ReadAllText(path);
        content.Should().Contain("_custom");
        content.Should().Contain("env");
        content.Should().Contain("test");
    }

    [Fact]
    public async Task HarStreamWriter_Custom_Null_NoCustomKeyInOutput()
    {
        var path = TempFile();

        await using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator, custom: null))
        {
            writer.WriteEntry(CreateEntry("https://example.com/api"));
            await writer.WaitForConsumerAsync();
        }

        var content = System.IO.File.ReadAllText(path);
        content.Should().NotContain("_custom");
    }
}
