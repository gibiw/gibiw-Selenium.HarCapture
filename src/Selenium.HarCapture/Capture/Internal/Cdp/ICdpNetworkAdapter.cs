using System;
using System.Threading.Tasks;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Abstraction over version-specific CDP Network domain.
/// Allows CdpNetworkCaptureStrategy to work with any supported CDP version (V142-V144).
/// </summary>
internal interface ICdpNetworkAdapter : IDisposable
{
    event Action<CdpRequestWillBeSentData>? RequestWillBeSent;
    event Action<CdpResponseReceivedData>? ResponseReceived;
    event Action<string>? LoadingFinished;
    event Action<string>? LoadingFailed;

    Task EnableNetworkAsync();
    Task DisableNetworkAsync();
    Task<(string? body, bool base64Encoded)> GetResponseBodyAsync(string requestId);
}
