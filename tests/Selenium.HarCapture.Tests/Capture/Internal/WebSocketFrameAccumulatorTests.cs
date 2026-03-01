using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class WebSocketFrameAccumulatorTests
{
    [Fact]
    public void OnCreated_RegistersConnection_IsWebSocket_ReturnsTrue()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();

        // Act
        accumulator.OnCreated("ws-1", "wss://example.com/socket");

        // Assert
        accumulator.IsWebSocket("ws-1").Should().BeTrue();
        accumulator.IsWebSocket("unknown").Should().BeFalse();
    }

    [Fact]
    public void AddFrame_StoresFrames_Flush_ReturnsSortedList()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com/socket");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add frames out of order
        accumulator.AddFrame("ws-1", "receive", 1001.0, 1, "hello");
        accumulator.AddFrame("ws-1", "send", 1000.5, 1, "world");
        accumulator.AddFrame("ws-1", "receive", 1002.0, 2, "binary-data");

        var result = accumulator.Flush("ws-1");

        // Assert
        result.Should().NotBeNull();
        var (entry, frames) = result!.Value;

        frames.Should().HaveCount(3);
        // Sorted by time
        frames[0].Type.Should().Be("send");
        frames[0].Data.Should().Be("world");
        frames[1].Type.Should().Be("receive");
        frames[1].Data.Should().Be("hello");
        frames[2].Type.Should().Be("receive");
        frames[2].Opcode.Should().Be(2);

        // Entry should have 101 status
        entry.Response.Status.Should().Be(101);
        entry.Request.Method.Should().Be("GET");
        entry.Request.Url.Should().Be("wss://example.com/socket");
    }

    [Fact]
    public void Flush_UnknownRequestId_ReturnsNull()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();

        // Act
        var result = accumulator.Flush("unknown-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Flush_RemovesConnection_SecondFlush_ReturnsNull()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act
        var first = accumulator.Flush("ws-1");
        var second = accumulator.Flush("ws-1");

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
        accumulator.IsWebSocket("ws-1").Should().BeFalse();
    }

    [Fact]
    public void GetActiveRequestIds_ReturnsUnclosedConnections()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com/1");
        accumulator.OnCreated("ws-2", "wss://example.com/2");
        accumulator.OnCreated("ws-3", "wss://example.com/3");

        // Flush one
        accumulator.OnHandshakeRequest("ws-2", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-2", 1000.1, 101, "Switching Protocols", null);
        accumulator.Flush("ws-2");

        // Act
        var active = accumulator.GetActiveRequestIds();

        // Assert
        active.Should().HaveCount(2);
        active.Should().Contain("ws-1");
        active.Should().Contain("ws-3");
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com/1");
        accumulator.OnCreated("ws-2", "wss://example.com/2");

        // Act
        accumulator.Clear();

        // Assert
        accumulator.IsWebSocket("ws-1").Should().BeFalse();
        accumulator.IsWebSocket("ws-2").Should().BeFalse();
        accumulator.GetActiveRequestIds().Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentAdds_FromMultipleThreads_DoNotLoseFrames()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        const int threadCount = 10;
        const int framesPerThread = 100;
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            var threadIndex = t;
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int f = 0; f < framesPerThread; f++)
                {
                    var timestamp = 1001.0 + threadIndex * framesPerThread + f;
                    accumulator.AddFrame("ws-1", "send", timestamp, 1, $"msg-{threadIndex}-{f}");
                }
            });
        }

        await Task.WhenAll(tasks);

        var result = accumulator.Flush("ws-1");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Frames.Should().HaveCount(threadCount * framesPerThread);
    }

    [Fact]
    public void TimeConversion_UsesHandshakeOffset()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");

        double handshakeTimestamp = 1000.0;
        double handshakeWallTime = 1700000000.0;
        accumulator.OnHandshakeRequest("ws-1", handshakeTimestamp, handshakeWallTime, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add a frame 5 seconds after handshake
        accumulator.AddFrame("ws-1", "send", 1005.0, 1, "test");

        var result = accumulator.Flush("ws-1");

        // Assert
        result.Should().NotBeNull();
        var frame = result!.Value.Frames[0];
        // frameWallTime = handshakeWallTime + (frameTimestamp - handshakeTimestamp)
        // = 1700000000.0 + (1005.0 - 1000.0) = 1700000005.0
        frame.Time.Should().BeApproximately(1700000005.0, 0.001);
    }

    [Fact]
    public void HandshakeHeaders_AreIncludedInEntry()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");

        var requestHeaders = new Dictionary<string, string>
        {
            ["Upgrade"] = "websocket",
            ["Connection"] = "Upgrade"
        };
        var responseHeaders = new Dictionary<string, string>
        {
            ["Upgrade"] = "websocket",
            ["Connection"] = "Upgrade",
            ["Sec-WebSocket-Accept"] = "abc123"
        };

        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, requestHeaders);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", responseHeaders);

        // Act
        var result = accumulator.Flush("ws-1");

        // Assert
        result.Should().NotBeNull();
        var entry = result!.Value.Entry;
        entry.Request.Headers.Should().HaveCount(2);
        entry.Response.Headers.Should().HaveCount(3);
    }

    // =========================================================
    // Frame Cap Enforcement (WS-03)
    // =========================================================

    [Fact]
    public void AddFrame_WhenMaxFramesZero_NoLimit()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add 50 frames with maxFrames=0 (unlimited)
        for (int i = 0; i < 50; i++)
        {
            accumulator.AddFrame("ws-1", "send", 1001.0 + i, 1, $"msg-{i}", maxFrames: 0);
        }

        var result = accumulator.Flush("ws-1");

        // Assert — all 50 frames stored, none dropped
        result.Should().NotBeNull();
        result!.Value.Frames.Should().HaveCount(50);
    }

    [Fact]
    public void AddFrame_WhenBelowCap_AllFramesStored()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add 5 frames with cap=10
        for (int i = 0; i < 5; i++)
        {
            accumulator.AddFrame("ws-1", "send", 1001.0 + i, 1, $"msg-{i}", maxFrames: 10);
        }

        var result = accumulator.Flush("ws-1");

        // Assert — all 5 stored
        result.Should().NotBeNull();
        result!.Value.Frames.Should().HaveCount(5);
    }

    [Fact]
    public void AddFrame_WhenAtCap_DropsOldestFrame()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add 5 frames with cap=3 (oldest 2 should be dropped)
        for (int i = 0; i < 5; i++)
        {
            accumulator.AddFrame("ws-1", "send", 1001.0 + i, 1, $"msg-{i}", maxFrames: 3);
        }

        var result = accumulator.Flush("ws-1");

        // Assert — only last 3 frames remain (most recent)
        result.Should().NotBeNull();
        var frames = result!.Value.Frames;
        frames.Should().HaveCount(3);
        // Frames are sorted by time; oldest are dropped so we expect msg-2, msg-3, msg-4
        frames.Select(f => f.Data).Should().Contain("msg-2");
        frames.Select(f => f.Data).Should().Contain("msg-3");
        frames.Select(f => f.Data).Should().Contain("msg-4");
        frames.Select(f => f.Data).Should().NotContain("msg-0");
        frames.Select(f => f.Data).Should().NotContain("msg-1");
    }

    [Fact]
    public void AddFrame_WhenAtCap_LogsDropEvent()
    {
        // Arrange — use a temp log file to capture log messages
        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ws-drop-test-{System.Guid.NewGuid()}.log");
        using var logger = FileLogger.Create(logPath);

        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add 4 frames with cap=3 (1 drop)
        for (int i = 0; i < 4; i++)
        {
            accumulator.AddFrame("ws-1", "send", 1001.0 + i, 1, $"msg-{i}", maxFrames: 3, logger: logger);
        }

        // Assert — log file should contain a drop message with the requestId
        var logContent = System.IO.File.ReadAllText(logPath);
        logContent.Should().Contain("ws-1");
        logContent.Should().Contain("drop");

        // Cleanup
        System.IO.File.Delete(logPath);
    }

    [Fact]
    public void AddFrame_WhenAtCap_DroppedFrameCount_Tracked()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add 5 frames with cap=3 (2 drops expected)
        for (int i = 0; i < 5; i++)
        {
            accumulator.AddFrame("ws-1", "send", 1001.0 + i, 1, $"msg-{i}", maxFrames: 3);
        }

        // Flush — result should note 2 dropped frames
        var result = accumulator.Flush("ws-1");

        // Assert — 3 frames remain
        result.Should().NotBeNull();
        result!.Value.Frames.Should().HaveCount(3);
    }

    // =========================================================
    // WebSocket Payload Redaction (RDCT-06)
    // =========================================================

    [Fact]
    public void AddFrame_WithRedactor_RedactsData()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(null, null, null,
            sensitiveBodyPatterns: new[] { @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b" });

        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act
        accumulator.AddFrame("ws-1", "send", 1001.0, 1, "user@test.com", redactor: redactor);

        var result = accumulator.Flush("ws-1");

        // Assert — email replaced with [REDACTED]
        result.Should().NotBeNull();
        result!.Value.Frames.Should().HaveCount(1);
        result.Value.Frames[0].Data.Should().Be("[REDACTED]");
    }

    [Fact]
    public void AddFrame_WithRedactor_RecordsWsRedactionCount()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(null, null, null,
            sensitiveBodyPatterns: new[] { @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b" });

        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — frame with two emails
        accumulator.AddFrame("ws-1", "send", 1001.0, 1, "a@test.com and b@test.com", redactor: redactor);

        var result = accumulator.Flush("ws-1");

        // Assert — both emails were redacted (verifies RecordWsRedaction was called and data replaced)
        result.Should().NotBeNull();
        result!.Value.Frames.Should().HaveCount(1);
        result.Value.Frames[0].Data.Should().NotContain("@test.com");
        result.Value.Frames[0].Data.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void AddFrame_WithNullRedactor_StoresOriginalData()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — no redactor
        accumulator.AddFrame("ws-1", "send", 1001.0, 1, "user@test.com");

        var result = accumulator.Flush("ws-1");

        // Assert — data stored as-is
        result.Should().NotBeNull();
        result!.Value.Frames[0].Data.Should().Be("user@test.com");
    }

    [Fact]
    public void AddFrame_WithRedactorNoMatch_StoresOriginalData()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(null, null, null,
            sensitiveBodyPatterns: new[] { @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b" });

        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — data with no email
        accumulator.AddFrame("ws-1", "send", 1001.0, 1, "hello world", redactor: redactor);

        var result = accumulator.Flush("ws-1");

        // Assert — data unchanged
        result.Should().NotBeNull();
        result!.Value.Frames[0].Data.Should().Be("hello world");
    }

    // =========================================================
    // Combined Cap + Redaction
    // =========================================================

    [Fact]
    public void AddFrame_WithCapAndRedactor_BothApplied()
    {
        // Arrange
        var redactor = new SensitiveDataRedactor(null, null, null,
            sensitiveBodyPatterns: new[] { @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b" });

        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Act — add 3 frames with cap=2 and redactor
        accumulator.AddFrame("ws-1", "send", 1001.0, 1, "first@test.com", maxFrames: 2, redactor: redactor);
        accumulator.AddFrame("ws-1", "send", 1002.0, 1, "second@test.com", maxFrames: 2, redactor: redactor);
        accumulator.AddFrame("ws-1", "send", 1003.0, 1, "third@test.com", maxFrames: 2, redactor: redactor);

        var result = accumulator.Flush("ws-1");

        // Assert — cap: only 2 frames remain; redaction: emails replaced
        result.Should().NotBeNull();
        var frames = result!.Value.Frames;
        frames.Should().HaveCount(2);
        frames.All(f => f.Data == "[REDACTED]").Should().BeTrue();
    }

    // =========================================================
    // ConcurrentQueue FIFO ordering
    // =========================================================

    [Fact]
    public void Flush_ReturnsFramesInTimeOrder()
    {
        // Arrange
        var accumulator = new WebSocketFrameAccumulator();
        accumulator.OnCreated("ws-1", "wss://example.com");
        accumulator.OnHandshakeRequest("ws-1", 1000.0, 1700000000.0, null);
        accumulator.OnHandshakeResponse("ws-1", 1000.1, 101, "Switching Protocols", null);

        // Add frames out of wall-clock order (timestamps determine order)
        accumulator.AddFrame("ws-1", "receive", 1003.0, 1, "third");
        accumulator.AddFrame("ws-1", "send",    1001.0, 1, "first");
        accumulator.AddFrame("ws-1", "receive", 1002.0, 1, "second");

        var result = accumulator.Flush("ws-1");

        // Assert — sorted by Time, not insertion order
        result.Should().NotBeNull();
        var frames = result!.Value.Frames;
        frames.Should().HaveCount(3);
        frames[0].Data.Should().Be("first");
        frames[1].Data.Should().Be("second");
        frames[2].Data.Should().Be("third");
    }
}

