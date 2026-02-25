using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _tempDir;

    public FileLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileLoggerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Create_ReturnsNull_WhenPathIsNull()
    {
        var logger = FileLogger.Create(null);

        logger.Should().BeNull();
    }

    [Fact]
    public void Log_WritesTimestampedLine_ToFile()
    {
        var path = Path.Combine(_tempDir, "test.log");
        using var logger = FileLogger.Create(path)!;

        logger.Log("TestCat", "hello world");

        var lines = File.ReadAllLines(path);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("[TestCat]");
        lines[0].Should().Contain("hello world");
        // Verify timestamp format (yyyy-MM-dd HH:mm:ss.fff)
        lines[0].Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[TestCat\] hello world$");
    }

    [Fact]
    public void Log_AppendsToExistingFile()
    {
        var path = Path.Combine(_tempDir, "append.log");
        File.WriteAllText(path, "existing line\n");

        using var logger = FileLogger.Create(path)!;
        logger.Log("Cat", "new line");

        var content = File.ReadAllText(path);
        content.Should().StartWith("existing line");
        content.Should().Contain("[Cat] new line");
    }

    [Fact]
    public void Log_IsThreadSafe_UnderParallelWrites()
    {
        var path = Path.Combine(_tempDir, "threadsafe.log");
        const int count = 200;

        using (var logger = FileLogger.Create(path)!)
        {
            Parallel.For(0, count, i =>
            {
                logger.Log("Thread", $"message {i}");
            });
        }

        var lines = File.ReadAllLines(path);
        lines.Should().HaveCount(count);
        foreach (var line in lines)
        {
            line.Should().Contain("[Thread]");
        }
    }
}
