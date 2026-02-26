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
}
