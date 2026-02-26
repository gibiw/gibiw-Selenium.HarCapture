using System;

namespace Selenium.HarCapture.Capture;

/// <summary>
/// Specifies the types of HTTP traffic data to capture during HAR recording.
/// Can be combined using bitwise OR operations to capture multiple data types.
/// </summary>
[Flags]
public enum CaptureType
{
    /// <summary>
    /// No data is captured. Use this to explicitly disable all capture.
    /// </summary>
    None = 0,

    /// <summary>
    /// Capture HTTP request headers.
    /// </summary>
    RequestHeaders = 1 << 0, // 1

    /// <summary>
    /// Capture HTTP request cookies.
    /// </summary>
    RequestCookies = 1 << 1, // 2

    /// <summary>
    /// Capture HTTP request body content (text-based content).
    /// </summary>
    RequestContent = 1 << 2, // 4

    /// <summary>
    /// Capture HTTP request binary content (images, files, etc.).
    /// </summary>
    RequestBinaryContent = 1 << 3, // 8

    /// <summary>
    /// Capture HTTP response headers.
    /// </summary>
    ResponseHeaders = 1 << 4, // 16

    /// <summary>
    /// Capture HTTP response cookies.
    /// </summary>
    ResponseCookies = 1 << 5, // 32

    /// <summary>
    /// Capture HTTP response body content (text-based content).
    /// </summary>
    ResponseContent = 1 << 6, // 64

    /// <summary>
    /// Capture HTTP response binary content (images, files, etc.).
    /// </summary>
    ResponseBinaryContent = 1 << 7, // 128

    /// <summary>
    /// Capture detailed request/response timing information.
    /// </summary>
    Timings = 1 << 8, // 256

    /// <summary>
    /// Capture connection information (server IP address, connection ID).
    /// </summary>
    ConnectionInfo = 1 << 9, // 512

    /// <summary>
    /// Capture WebSocket frames (sent and received messages after the handshake).
    /// Requires CDP strategy (Chrome/Edge). INetwork strategy silently ignores this flag.
    /// </summary>
    WebSocket = 1 << 10, // 1024

    /// <summary>
    /// Convenience combination: Capture all headers and cookies for both requests and responses.
    /// Equivalent to RequestHeaders | RequestCookies | ResponseHeaders | ResponseCookies.
    /// </summary>
    HeadersAndCookies = RequestHeaders | RequestCookies | ResponseHeaders | ResponseCookies,

    /// <summary>
    /// Convenience combination: Capture all text-based content but exclude binary content.
    /// Equivalent to HeadersAndCookies | RequestContent | ResponseContent | Timings.
    /// </summary>
    AllText = HeadersAndCookies | RequestContent | ResponseContent | Timings,

    /// <summary>
    /// Convenience combination: Capture everything including binary content, timings, connection info, and WebSocket frames.
    /// Equivalent to all individual flags combined.
    /// </summary>
    All = RequestHeaders | RequestCookies | RequestContent | RequestBinaryContent |
          ResponseHeaders | ResponseCookies | ResponseContent | ResponseBinaryContent |
          Timings | ConnectionInfo | WebSocket
}
