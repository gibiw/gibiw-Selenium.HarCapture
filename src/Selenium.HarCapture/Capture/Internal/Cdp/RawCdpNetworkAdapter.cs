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

        ResponseReceived?.Invoke(new CdpResponseReceivedData
        {
            RequestId = requestId,
            Timestamp = timestamp,
            Response = ParseResponse(response)
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
