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

public sealed class HarStreamWriterTests : IDisposable
{
    private readonly string _tempDir;

    public HarStreamWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"HarStreamWriterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

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
    public void WriteEntry_SingleEntry_ProducesValidHar()
    {
        var path = TempFile();
        var entry = CreateEntry("https://example.com/api");

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            writer.WriteEntry(entry);
            writer.Count.Should().Be(1);
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(1);
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/api");
    }

    [Fact]
    public void WriteEntry_MultipleEntries_ProducesValidHar()
    {
        var path = TempFile();

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
        {
            for (int i = 0; i < 10; i++)
            {
                writer.WriteEntry(CreateEntry($"https://example.com/{i}", 200 + i));
            }

            writer.Count.Should().Be(10);
        }

        var har = HarSerializer.Load(path);
        har.Log.Entries.Should().HaveCount(10);
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/0");
        har.Log.Entries[9].Request.Url.Should().Be("https://example.com/9");
        har.Log.Entries[9].Response.Status.Should().Be(209);
    }

    [Fact]
    public void AlwaysValid_FileIsValidAfterEachEntry_WithoutComplete()
    {
        var path = TempFile();

        // Write 5 entries without calling Complete or Dispose
        var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 65536);
        var writer = new HarStreamWriter(path, "1.2", DefaultCreator);

        for (int i = 1; i <= 5; i++)
        {
            writer.WriteEntry(CreateEntry($"https://example.com/{i}"));

            // Read the file while writer still holds it (via FileShare.Read on writer's stream)
            var content = File.ReadAllText(path);
            var har = HarSerializer.Deserialize(content);
            har.Log.Entries.Should().HaveCount(i, $"after writing {i} entries the file should be valid with {i} entries");
        }

        writer.Dispose();
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
        har.Log.Pages[0].Id.Should().Be("page1");
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
        har.Log.Pages[0].Id.Should().Be("init");
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
    public void ConcurrentWriteEntry_AllEntriesPresent()
    {
        var path = TempFile();
        const int threadCount = 8;
        const int entriesPerThread = 50;

        using (var writer = new HarStreamWriter(path, "1.2", DefaultCreator))
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
}
