using System;
using System.Linq;
using System.Threading.Tasks;
using OpenQA.Selenium.DevTools;
using DevToolsSessionDomains = OpenQA.Selenium.DevTools.V143.DevToolsSessionDomains;
using Network = OpenQA.Selenium.DevTools.V143.Network;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// CDP Network adapter for Chrome DevTools Protocol V143.
/// </summary>
internal sealed class CdpNetworkAdapterV143 : ICdpNetworkAdapter
{
    private readonly DevToolsSessionDomains _domains;
    private bool _disposed;

    public event Action<CdpRequestWillBeSentData>? RequestWillBeSent;
    public event Action<CdpResponseReceivedData>? ResponseReceived;
    public event Action<string>? LoadingFinished;
    public event Action<string>? LoadingFailed;

    internal CdpNetworkAdapterV143(DevToolsSession session)
    {
        _domains = session.GetVersionSpecificDomains<DevToolsSessionDomains>();
        _domains.Network.RequestWillBeSent += OnRequestWillBeSent;
        _domains.Network.ResponseReceived += OnResponseReceived;
        _domains.Network.LoadingFinished += OnLoadingFinished;
        _domains.Network.LoadingFailed += OnLoadingFailed;
    }

    public Task EnableNetworkAsync()
        => _domains.Network.Enable(new Network.EnableCommandSettings());

    public Task DisableNetworkAsync()
        => _domains.Network.Disable(new Network.DisableCommandSettings());

    public async Task<(string? body, bool base64Encoded)> GetResponseBodyAsync(string requestId)
    {
        var response = await _domains.Network.GetResponseBody(
            new Network.GetResponseBodyCommandSettings { RequestId = requestId }).ConfigureAwait(false);
        return (response.Body, response.Base64Encoded);
    }

    private void OnRequestWillBeSent(object? sender, Network.RequestWillBeSentEventArgs e)
    {
        RequestWillBeSent?.Invoke(new CdpRequestWillBeSentData
        {
            RequestId = e.RequestId,
            WallTime = e.WallTime,
            Timestamp = e.Timestamp,
            Request = MapRequest(e.Request),
            RedirectResponse = e.RedirectResponse != null ? MapResponse(e.RedirectResponse) : null
        });
    }

    private void OnResponseReceived(object? sender, Network.ResponseReceivedEventArgs e)
    {
        ResponseReceived?.Invoke(new CdpResponseReceivedData
        {
            RequestId = e.RequestId,
            Timestamp = e.Timestamp,
            Response = MapResponse(e.Response)
        });
    }

    private void OnLoadingFinished(object? sender, Network.LoadingFinishedEventArgs e)
        => LoadingFinished?.Invoke(e.RequestId);

    private void OnLoadingFailed(object? sender, Network.LoadingFailedEventArgs e)
        => LoadingFailed?.Invoke(e.RequestId);

    private static CdpRequestInfo MapRequest(Network.Request r) => new()
    {
        Url = r.Url,
        Method = r.Method,
        Headers = r.Headers?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        PostData = r.PostData
    };

    private static CdpResponseInfo MapResponse(Network.Response r) => new()
    {
        Status = r.Status,
        StatusText = r.StatusText,
        Protocol = r.Protocol,
        MimeType = r.MimeType,
        Headers = r.Headers?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        Timing = r.Timing != null ? MapTiming(r.Timing) : null
    };

    private static CdpTimingInfo MapTiming(Network.ResourceTiming t) => new()
    {
        RequestTime = t.RequestTime,
        DnsStart = t.DnsStart,
        DnsEnd = t.DnsEnd,
        ConnectStart = t.ConnectStart,
        ConnectEnd = t.ConnectEnd,
        SslStart = t.SslStart,
        SslEnd = t.SslEnd,
        SendStart = t.SendStart,
        SendEnd = t.SendEnd,
        ReceiveHeadersEnd = t.ReceiveHeadersEnd
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _domains.Network.RequestWillBeSent -= OnRequestWillBeSent;
        _domains.Network.ResponseReceived -= OnResponseReceived;
        _domains.Network.LoadingFinished -= OnLoadingFinished;
        _domains.Network.LoadingFailed -= OnLoadingFailed;
    }
}
