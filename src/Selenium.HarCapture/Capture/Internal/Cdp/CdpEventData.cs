using System.Collections.Generic;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Version-independent DTO for CDP Network.requestWillBeSent event data.
/// </summary>
internal sealed class CdpRequestWillBeSentData
{
    public string RequestId { get; init; } = null!;
    public CdpRequestInfo Request { get; init; } = null!;
    public double WallTime { get; init; }
    public double Timestamp { get; init; }
    public CdpResponseInfo? RedirectResponse { get; init; }
}

/// <summary>
/// Version-independent DTO for CDP Network.responseReceived event data.
/// </summary>
internal sealed class CdpResponseReceivedData
{
    public string RequestId { get; init; } = null!;
    public CdpResponseInfo Response { get; init; } = null!;
    public double Timestamp { get; init; }
    public string? Type { get; init; }
}

/// <summary>
/// Version-independent DTO mirroring CDP Network.Request.
/// </summary>
internal sealed class CdpRequestInfo
{
    public string Url { get; init; } = null!;
    public string? Method { get; init; }
    public IDictionary<string, string>? Headers { get; init; }
    public string? PostData { get; init; }
}

/// <summary>
/// Version-independent DTO mirroring CDP Network.Response.
/// </summary>
internal sealed class CdpResponseInfo
{
    public long Status { get; init; }
    public string? StatusText { get; init; }
    public string? Protocol { get; init; }
    public string? MimeType { get; init; }
    public IDictionary<string, string>? Headers { get; init; }
    public CdpTimingInfo? Timing { get; init; }
}

/// <summary>
/// Version-independent DTO for CDP Network.webSocketCreated event data.
/// </summary>
internal sealed class CdpWebSocketCreatedData
{
    public string RequestId { get; init; } = null!;
    public string Url { get; init; } = null!;
}

/// <summary>
/// Version-independent DTO for CDP Network.webSocketWillSendHandshakeRequest event data.
/// </summary>
internal sealed class CdpWebSocketHandshakeRequestData
{
    public string RequestId { get; init; } = null!;
    public double Timestamp { get; init; }
    public double WallTime { get; init; }
    public IDictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Version-independent DTO for CDP Network.webSocketHandshakeResponseReceived event data.
/// </summary>
internal sealed class CdpWebSocketHandshakeResponseData
{
    public string RequestId { get; init; } = null!;
    public double Timestamp { get; init; }
    public long Status { get; init; }
    public string? StatusText { get; init; }
    public IDictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Version-independent DTO for CDP Network.webSocketFrameSent / webSocketFrameReceived event data.
/// </summary>
internal sealed class CdpWebSocketFrameData
{
    public string RequestId { get; init; } = null!;
    public double Timestamp { get; init; }
    public int Opcode { get; init; }
    public string PayloadData { get; init; } = null!;
}

/// <summary>
/// Version-independent DTO for CDP Network.webSocketClosed event data.
/// </summary>
internal sealed class CdpWebSocketClosedData
{
    public string RequestId { get; init; } = null!;
    public double Timestamp { get; init; }
}

/// <summary>
/// Version-independent DTO mirroring CDP Network.ResourceTiming.
/// </summary>
internal sealed class CdpTimingInfo
{
    public double RequestTime { get; init; }
    public double DnsStart { get; init; }
    public double DnsEnd { get; init; }
    public double ConnectStart { get; init; }
    public double ConnectEnd { get; init; }
    public double SslStart { get; init; }
    public double SslEnd { get; init; }
    public double SendStart { get; init; }
    public double SendEnd { get; init; }
    public double ReceiveHeadersEnd { get; init; }
}
