using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using OpenQA.Selenium.DevTools;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Version-agnostic CDP Network adapter that uses raw DevToolsSession commands
/// instead of version-specific generated types.
/// Used as a fallback when ReflectiveCdpNetworkAdapter cannot find a compatible CDP version.
/// </summary>
internal sealed class RawCdpNetworkAdapter : ICdpNetworkAdapter
{
    private readonly DevToolsSession _session;
    private readonly FileLogger? _logger;
    private bool _disposed;

    public event Action<CdpRequestWillBeSentData>? RequestWillBeSent;
    public event Action<CdpResponseReceivedData>? ResponseReceived;
    public event Action<string>? LoadingFinished;
    public event Action<string>? LoadingFailed;

    public event Action<CdpWebSocketCreatedData>? WebSocketCreated;
    public event Action<CdpWebSocketHandshakeRequestData>? WebSocketWillSendHandshakeRequest;
    public event Action<CdpWebSocketHandshakeResponseData>? WebSocketHandshakeResponseReceived;
    public event Action<CdpWebSocketFrameData>? WebSocketFrameSent;
    public event Action<CdpWebSocketFrameData>? WebSocketFrameReceived;
    public event Action<CdpWebSocketClosedData>? WebSocketClosed;

    public event Action<CdpPageTimingEventData>? DomContentEventFired;
    public event Action<CdpPageTimingEventData>? LoadEventFired;

    internal RawCdpNetworkAdapter(DevToolsSession session, FileLogger? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger;
        _session.DevToolsEventReceived += OnDevToolsEventReceived;
    }

    public async Task EnableNetworkAsync()
    {
        await _session.SendCommand("Network.enable", new JsonObject()).ConfigureAwait(false);
    }

    public async Task DisableNetworkAsync()
    {
        await _session.SendCommand("Network.disable", new JsonObject()).ConfigureAwait(false);
    }

    public async Task<(string? body, bool base64Encoded)> GetResponseBodyAsync(string requestId)
    {
        var result = await _session.SendCommand(
            "Network.getResponseBody",
            new JsonObject { ["requestId"] = requestId }).ConfigureAwait(false);

        if (result == null)
        {
            return (null, false);
        }

        var body = result.Value.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
        var base64 = result.Value.TryGetProperty("base64Encoded", out var b64Prop) && b64Prop.GetBoolean();

        return (body, base64);
    }

    public Task EnablePageAsync() => Task.CompletedTask;

    public Task DisablePageAsync() => Task.CompletedTask;

    private void OnDevToolsEventReceived(object? sender, DevToolsEventReceivedEventArgs e)
    {
        if (e.DomainName != "Network")
        {
            return;
        }

        try
        {
            switch (e.EventName)
            {
                case "requestWillBeSent":
                    HandleRequestWillBeSent(e.EventData);
                    break;
                case "responseReceived":
                    HandleResponseReceived(e.EventData);
                    break;
                case "loadingFinished":
                    HandleLoadingFinished(e.EventData);
                    break;
                case "loadingFailed":
                    HandleLoadingFailed(e.EventData);
                    break;
                case "webSocketCreated":
                    HandleWebSocketCreated(e.EventData);
                    break;
                case "webSocketWillSendHandshakeRequest":
                    HandleWebSocketWillSendHandshakeRequest(e.EventData);
                    break;
                case "webSocketHandshakeResponseReceived":
                    HandleWebSocketHandshakeResponseReceived(e.EventData);
                    break;
                case "webSocketFrameSent":
                    HandleWebSocketFrameSent(e.EventData);
                    break;
                case "webSocketFrameReceived":
                    HandleWebSocketFrameReceived(e.EventData);
                    break;
                case "webSocketClosed":
                    HandleWebSocketClosed(e.EventData);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.Log("RawCDP", $"Error handling {e.EventName}: {ex.Message}");
        }
    }

    private void HandleRequestWillBeSent(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var request = data.GetProperty("request");
        var wallTime = data.GetProperty("wallTime").GetDouble();
        var timestamp = data.GetProperty("timestamp").GetDouble();

        CdpResponseInfo? redirectResponse = null;
        if (data.TryGetProperty("redirectResponse", out var redirectProp) &&
            redirectProp.ValueKind != JsonValueKind.Null &&
            redirectProp.ValueKind != JsonValueKind.Undefined)
        {
            redirectResponse = ParseResponse(redirectProp);
        }

        RequestWillBeSent?.Invoke(new CdpRequestWillBeSentData
        {
            RequestId = requestId,
            WallTime = wallTime,
            Timestamp = timestamp,
            Request = ParseRequest(request),
            RedirectResponse = redirectResponse
        });
    }

    private void HandleResponseReceived(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var timestamp = data.GetProperty("timestamp").GetDouble();
        var response = data.GetProperty("response");
        var type = data.TryGetProperty("type", out var typeProp) ? typeProp.GetString()?.ToLowerInvariant() : null;

        ResponseReceived?.Invoke(new CdpResponseReceivedData
        {
            RequestId = requestId,
            Timestamp = timestamp,
            Response = ParseResponse(response),
            Type = type
        });
    }

    private void HandleLoadingFinished(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        LoadingFinished?.Invoke(requestId);
    }

    private void HandleLoadingFailed(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        LoadingFailed?.Invoke(requestId);
    }

    private void HandleWebSocketCreated(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var url = data.TryGetProperty("url", out var urlProp) ? urlProp.GetString()! : "";

        WebSocketCreated?.Invoke(new CdpWebSocketCreatedData
        {
            RequestId = requestId,
            Url = url
        });
    }

    private void HandleWebSocketWillSendHandshakeRequest(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var timestamp = data.GetProperty("timestamp").GetDouble();
        var wallTime = data.TryGetProperty("wallTime", out var wt) ? wt.GetDouble() : timestamp;

        IDictionary<string, string>? headers = null;
        if (data.TryGetProperty("request", out var reqEl) &&
            reqEl.ValueKind == JsonValueKind.Object)
        {
            headers = ParseHeaders(reqEl);
        }

        WebSocketWillSendHandshakeRequest?.Invoke(new CdpWebSocketHandshakeRequestData
        {
            RequestId = requestId,
            Timestamp = timestamp,
            WallTime = wallTime,
            Headers = headers
        });
    }

    private void HandleWebSocketHandshakeResponseReceived(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var timestamp = data.GetProperty("timestamp").GetDouble();

        long status = 101;
        string? statusText = "Switching Protocols";
        IDictionary<string, string>? headers = null;

        if (data.TryGetProperty("response", out var respEl) &&
            respEl.ValueKind == JsonValueKind.Object)
        {
            if (respEl.TryGetProperty("status", out var sProp))
                status = sProp.GetInt64();
            if (respEl.TryGetProperty("statusText", out var stProp))
                statusText = stProp.GetString();
            headers = ParseHeaders(respEl);
        }

        WebSocketHandshakeResponseReceived?.Invoke(new CdpWebSocketHandshakeResponseData
        {
            RequestId = requestId,
            Timestamp = timestamp,
            Status = status,
            StatusText = statusText,
            Headers = headers
        });
    }

    private void HandleWebSocketFrameSent(JsonElement data)
    {
        WebSocketFrameSent?.Invoke(ParseWebSocketFrame(data));
    }

    private void HandleWebSocketFrameReceived(JsonElement data)
    {
        WebSocketFrameReceived?.Invoke(ParseWebSocketFrame(data));
    }

    private static CdpWebSocketFrameData ParseWebSocketFrame(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var timestamp = data.GetProperty("timestamp").GetDouble();

        var opcode = 1; // default to text
        var payloadData = "";

        if (data.TryGetProperty("response", out var respEl) &&
            respEl.ValueKind == JsonValueKind.Object)
        {
            if (respEl.TryGetProperty("opcode", out var opProp))
                opcode = opProp.GetInt32();
            if (respEl.TryGetProperty("payloadData", out var pdProp))
                payloadData = pdProp.GetString() ?? "";
        }

        return new CdpWebSocketFrameData
        {
            RequestId = requestId,
            Timestamp = timestamp,
            Opcode = opcode,
            PayloadData = payloadData
        };
    }

    private void HandleWebSocketClosed(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString()!;
        var timestamp = data.GetProperty("timestamp").GetDouble();

        WebSocketClosed?.Invoke(new CdpWebSocketClosedData
        {
            RequestId = requestId,
            Timestamp = timestamp
        });
    }

    private static CdpRequestInfo ParseRequest(JsonElement request)
    {
        return new CdpRequestInfo
        {
            Url = request.GetProperty("url").GetString()!,
            Method = request.TryGetProperty("method", out var m) ? m.GetString() : null,
            Headers = ParseHeaders(request),
            PostData = request.TryGetProperty("postData", out var pd) ? pd.GetString() : null
        };
    }

    private static CdpResponseInfo ParseResponse(JsonElement response)
    {
        CdpTimingInfo? timing = null;
        if (response.TryGetProperty("timing", out var timingEl) &&
            timingEl.ValueKind != JsonValueKind.Null &&
            timingEl.ValueKind != JsonValueKind.Undefined)
        {
            timing = ParseTiming(timingEl);
        }

        return new CdpResponseInfo
        {
            Status = response.GetProperty("status").GetInt64(),
            StatusText = response.TryGetProperty("statusText", out var st) ? st.GetString() : null,
            Protocol = response.TryGetProperty("protocol", out var p) ? p.GetString() : null,
            MimeType = response.TryGetProperty("mimeType", out var mt) ? mt.GetString() : null,
            Headers = ParseHeaders(response),
            Timing = timing
        };
    }

    private static CdpTimingInfo ParseTiming(JsonElement timing)
    {
        return new CdpTimingInfo
        {
            RequestTime = GetDouble(timing, "requestTime"),
            DnsStart = GetDouble(timing, "dnsStart"),
            DnsEnd = GetDouble(timing, "dnsEnd"),
            ConnectStart = GetDouble(timing, "connectStart"),
            ConnectEnd = GetDouble(timing, "connectEnd"),
            SslStart = GetDouble(timing, "sslStart"),
            SslEnd = GetDouble(timing, "sslEnd"),
            SendStart = GetDouble(timing, "sendStart"),
            SendEnd = GetDouble(timing, "sendEnd"),
            ReceiveHeadersEnd = GetDouble(timing, "receiveHeadersEnd")
        };
    }

    private static IDictionary<string, string>? ParseHeaders(JsonElement parent)
    {
        if (!parent.TryGetProperty("headers", out var headersEl) ||
            headersEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var headers = new Dictionary<string, string>();
        foreach (var prop in headersEl.EnumerateObject())
        {
            headers[prop.Name] = prop.Value.GetString() ?? "";
        }

        return headers;
    }

    private static double GetDouble(JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) ? prop.GetDouble() : -1;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.DevToolsEventReceived -= OnDevToolsEventReceived;
    }
}
