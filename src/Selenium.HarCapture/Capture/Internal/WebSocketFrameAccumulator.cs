using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Selenium.HarCapture.Capture.Internal.Cdp;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Thread-safe accumulator for WebSocket frames.
/// Holds handshake entries internally until the socket closes or capture stops,
/// then emits complete entries with all accumulated frames attached.
/// </summary>
internal sealed class WebSocketFrameAccumulator
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

    /// <summary>
    /// Registers a new WebSocket connection.
    /// </summary>
    public void OnCreated(string requestId, string url)
    {
        _connections.TryAdd(requestId, new WebSocketConnection { Url = url });
    }

    /// <summary>
    /// Returns true if the given requestId is a known WebSocket connection.
    /// </summary>
    public bool IsWebSocket(string requestId)
    {
        return _connections.ContainsKey(requestId);
    }

    /// <summary>
    /// Stores handshake request data (wallTime, timestamp, headers) for time conversion and HarRequest building.
    /// </summary>
    public void OnHandshakeRequest(string requestId, double timestamp, double wallTime, IDictionary<string, string>? headers)
    {
        if (!_connections.TryGetValue(requestId, out var conn))
            return;

        conn.HandshakeTimestamp = timestamp;
        conn.HandshakeWallTime = wallTime;
        conn.RequestHeaders = headers;
    }

    /// <summary>
    /// Stores handshake response data for building the base HarEntry.
    /// </summary>
    public void OnHandshakeResponse(string requestId, double timestamp, long status, string? statusText, IDictionary<string, string>? headers)
    {
        if (!_connections.TryGetValue(requestId, out var conn))
            return;

        conn.ResponseTimestamp = timestamp;
        conn.ResponseStatus = status;
        conn.ResponseStatusText = statusText;
        conn.ResponseHeaders = headers;
    }

    /// <summary>
    /// Accumulates a WebSocket frame. Converts CDP timestamp to wall clock time.
    /// </summary>
    public void AddFrame(string requestId, string type, double timestamp, int opcode, string data)
    {
        if (!_connections.TryGetValue(requestId, out var conn))
            return;

        // Convert CDP monotonic timestamp to wall clock (epoch seconds)
        var wallTime = conn.HandshakeWallTime + (timestamp - conn.HandshakeTimestamp);

        conn.Frames.Add(new HarWebSocketMessage
        {
            Type = type,
            Time = wallTime,
            Opcode = opcode,
            Data = data
        });
    }

    /// <summary>
    /// Flushes a WebSocket connection: returns the base HarEntry with sorted frames attached, then removes it.
    /// Returns null if the requestId is unknown.
    /// </summary>
    public (HarEntry Entry, List<HarWebSocketMessage> Frames)? Flush(string requestId)
    {
        if (!_connections.TryRemove(requestId, out var conn))
            return null;

        var entry = BuildEntry(conn);
        var frames = conn.Frames.ToList();
        frames.Sort((a, b) => a.Time.CompareTo(b.Time));

        return (entry, frames);
    }

    /// <summary>
    /// Returns the request IDs of all unclosed WebSocket connections.
    /// </summary>
    public ICollection<string> GetActiveRequestIds()
    {
        return _connections.Keys.ToList();
    }

    /// <summary>
    /// Removes all connections and accumulated data.
    /// </summary>
    public void Clear()
    {
        _connections.Clear();
    }

    private static HarEntry BuildEntry(WebSocketConnection conn)
    {
        var startedDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(conn.HandshakeWallTime * 1000));

        var requestHeaders = new List<HarHeader>();
        if (conn.RequestHeaders != null)
        {
            foreach (var kvp in conn.RequestHeaders)
                requestHeaders.Add(new HarHeader { Name = kvp.Key, Value = kvp.Value });
        }

        var responseHeaders = new List<HarHeader>();
        if (conn.ResponseHeaders != null)
        {
            foreach (var kvp in conn.ResponseHeaders)
                responseHeaders.Add(new HarHeader { Name = kvp.Key, Value = kvp.Value });
        }

        return new HarEntry
        {
            StartedDateTime = startedDateTime,
            Time = conn.ResponseTimestamp > 0
                ? (conn.ResponseTimestamp - conn.HandshakeTimestamp) * 1000
                : 0,
            Request = new HarRequest
            {
                Method = "GET",
                Url = conn.Url,
                HttpVersion = "HTTP/1.1",
                Headers = requestHeaders,
                Cookies = new List<HarCookie>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = 0
            },
            Response = new HarResponse
            {
                Status = (int)conn.ResponseStatus,
                StatusText = conn.ResponseStatusText ?? "Switching Protocols",
                HttpVersion = "HTTP/1.1",
                Headers = responseHeaders,
                Cookies = new List<HarCookie>(),
                Content = new HarContent
                {
                    Size = 0,
                    MimeType = "x-unknown"
                },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = 0
            },
            Cache = new HarCache(),
            Timings = new HarTimings
            {
                Send = 0,
                Wait = 0,
                Receive = 0
            }
        };
    }

    private sealed class WebSocketConnection
    {
        public string Url { get; set; } = "";
        public double HandshakeTimestamp { get; set; }
        public double HandshakeWallTime { get; set; }
        public IDictionary<string, string>? RequestHeaders { get; set; }
        public double ResponseTimestamp { get; set; }
        public long ResponseStatus { get; set; } = 101;
        public string? ResponseStatusText { get; set; } = "Switching Protocols";
        public IDictionary<string, string>? ResponseHeaders { get; set; }
        public ConcurrentBag<HarWebSocketMessage> Frames { get; } = new();
    }
}
