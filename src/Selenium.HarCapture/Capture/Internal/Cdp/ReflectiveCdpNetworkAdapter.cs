using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using OpenQA.Selenium.DevTools;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Reflection-based CDP Network adapter that works with any CDP version.
/// Uses reflection and expression trees to interact with version-specific types
/// without compile-time dependencies on specific CDP version namespaces.
/// </summary>
internal sealed class ReflectiveCdpNetworkAdapter : ICdpNetworkAdapter
{
    private readonly object _network;
    private readonly MethodInfo _enableMethod;
    private readonly MethodInfo _disableMethod;
    private readonly MethodInfo _getResponseBodyMethod;
    private readonly Type _enableSettingsType;
    private readonly Type _disableSettingsType;
    private readonly Type _getResponseBodySettingsType;
    private readonly PropertyInfo _requestIdProperty;

    private readonly Delegate _requestWillBeSentHandler;
    private readonly Delegate _responseReceivedHandler;
    private readonly Delegate _loadingFinishedHandler;
    private readonly Delegate _loadingFailedHandler;

    private readonly EventInfo _requestWillBeSentEvent;
    private readonly EventInfo _responseReceivedEvent;
    private readonly EventInfo _loadingFinishedEvent;
    private readonly EventInfo _loadingFailedEvent;

    private bool _disposed;

    public event Action<CdpRequestWillBeSentData>? RequestWillBeSent;
    public event Action<CdpResponseReceivedData>? ResponseReceived;
    public event Action<string>? LoadingFinished;
    public event Action<string>? LoadingFailed;

    internal ReflectiveCdpNetworkAdapter(DevToolsSession session, Type domainsType)
    {
        // Call session.GetVersionSpecificDomains<T>() via reflection
        var getDomainsMethod = typeof(DevToolsSession)
            .GetMethod(nameof(DevToolsSession.GetVersionSpecificDomains))!
            .MakeGenericMethod(domainsType);

        var domains = getDomainsMethod.Invoke(session, null)!;

        // Get the Network property from domains
        var networkProperty = domainsType.GetProperty("Network")
            ?? throw new InvalidOperationException($"Type {domainsType.FullName} does not have a Network property.");
        _network = networkProperty.GetValue(domains)!;

        var networkType = _network.GetType();
        var networkNamespace = networkType.Namespace!;

        // Resolve command settings types
        _enableSettingsType = ResolveType(networkType.Assembly, networkNamespace, "EnableCommandSettings");
        _disableSettingsType = ResolveType(networkType.Assembly, networkNamespace, "DisableCommandSettings");
        _getResponseBodySettingsType = ResolveType(networkType.Assembly, networkNamespace, "GetResponseBodyCommandSettings");
        _requestIdProperty = _getResponseBodySettingsType.GetProperty("RequestId")
            ?? throw new InvalidOperationException("GetResponseBodyCommandSettings.RequestId not found.");

        // Resolve methods on the Network domain
        _enableMethod = networkType.GetMethod("Enable", new[] { _enableSettingsType })
            ?? throw new InvalidOperationException("Network.Enable method not found.");
        _disableMethod = networkType.GetMethod("Disable", new[] { _disableSettingsType })
            ?? throw new InvalidOperationException("Network.Disable method not found.");
        _getResponseBodyMethod = networkType.GetMethod("GetResponseBody", new[] { _getResponseBodySettingsType })
            ?? throw new InvalidOperationException("Network.GetResponseBody method not found.");

        // Resolve events and subscribe
        _requestWillBeSentEvent = networkType.GetEvent("RequestWillBeSent")!;
        _responseReceivedEvent = networkType.GetEvent("ResponseReceived")!;
        _loadingFinishedEvent = networkType.GetEvent("LoadingFinished")!;
        _loadingFailedEvent = networkType.GetEvent("LoadingFailed")!;

        _requestWillBeSentHandler = SubscribeEvent(_requestWillBeSentEvent, OnRequestWillBeSent);
        _responseReceivedHandler = SubscribeEvent(_responseReceivedEvent, OnResponseReceived);
        _loadingFinishedHandler = SubscribeEvent(_loadingFinishedEvent, OnLoadingFinished);
        _loadingFailedHandler = SubscribeEvent(_loadingFailedEvent, OnLoadingFailed);
    }

    public Task EnableNetworkAsync()
    {
        var settings = Activator.CreateInstance(_enableSettingsType)!;
        return (Task)_enableMethod.Invoke(_network, new[] { settings })!;
    }

    public Task DisableNetworkAsync()
    {
        var settings = Activator.CreateInstance(_disableSettingsType)!;
        return (Task)_disableMethod.Invoke(_network, new[] { settings })!;
    }

    public async Task<(string? body, bool base64Encoded)> GetResponseBodyAsync(string requestId)
    {
        var settings = Activator.CreateInstance(_getResponseBodySettingsType)!;
        _requestIdProperty.SetValue(settings, requestId);

        var task = (Task)_getResponseBodyMethod.Invoke(_network, new[] { settings })!;
        await task.ConfigureAwait(false);

        // Extract the Result property from Task<T>
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var body = (string?)result.GetType().GetProperty("Body")!.GetValue(result);
        var base64Encoded = (bool)result.GetType().GetProperty("Base64Encoded")!.GetValue(result)!;

        return (body, base64Encoded);
    }

    private void OnRequestWillBeSent(object eventArgs)
    {
        var type = eventArgs.GetType();
        var requestObj = type.GetProperty("Request")!.GetValue(eventArgs);
        var redirectResponseObj = type.GetProperty("RedirectResponse")!.GetValue(eventArgs);

        RequestWillBeSent?.Invoke(new CdpRequestWillBeSentData
        {
            RequestId = (string)type.GetProperty("RequestId")!.GetValue(eventArgs)!,
            WallTime = (double)type.GetProperty("WallTime")!.GetValue(eventArgs)!,
            Timestamp = (double)type.GetProperty("Timestamp")!.GetValue(eventArgs)!,
            Request = MapRequest(requestObj!),
            RedirectResponse = redirectResponseObj != null ? MapResponse(redirectResponseObj) : null
        });
    }

    private void OnResponseReceived(object eventArgs)
    {
        var type = eventArgs.GetType();
        var responseObj = type.GetProperty("Response")!.GetValue(eventArgs);

        ResponseReceived?.Invoke(new CdpResponseReceivedData
        {
            RequestId = (string)type.GetProperty("RequestId")!.GetValue(eventArgs)!,
            Timestamp = (double)type.GetProperty("Timestamp")!.GetValue(eventArgs)!,
            Response = MapResponse(responseObj!)
        });
    }

    private void OnLoadingFinished(object eventArgs)
    {
        var requestId = (string)eventArgs.GetType().GetProperty("RequestId")!.GetValue(eventArgs)!;
        LoadingFinished?.Invoke(requestId);
    }

    private void OnLoadingFailed(object eventArgs)
    {
        var requestId = (string)eventArgs.GetType().GetProperty("RequestId")!.GetValue(eventArgs)!;
        LoadingFailed?.Invoke(requestId);
    }

    private static CdpRequestInfo MapRequest(object r)
    {
        var type = r.GetType();
        var headers = type.GetProperty("Headers")!.GetValue(r);
        return new CdpRequestInfo
        {
            Url = (string)type.GetProperty("Url")!.GetValue(r)!,
            Method = (string?)type.GetProperty("Method")!.GetValue(r),
            Headers = CastHeaders(headers),
            PostData = (string?)type.GetProperty("PostData")!.GetValue(r)
        };
    }

    private static CdpResponseInfo MapResponse(object r)
    {
        var type = r.GetType();
        var headers = type.GetProperty("Headers")!.GetValue(r);
        var timing = type.GetProperty("Timing")!.GetValue(r);
        return new CdpResponseInfo
        {
            Status = (long)type.GetProperty("Status")!.GetValue(r)!,
            StatusText = (string?)type.GetProperty("StatusText")!.GetValue(r),
            Protocol = (string?)type.GetProperty("Protocol")!.GetValue(r),
            MimeType = (string?)type.GetProperty("MimeType")!.GetValue(r),
            Headers = CastHeaders(headers),
            Timing = timing != null ? MapTiming(timing) : null
        };
    }

    private static CdpTimingInfo MapTiming(object t)
    {
        var type = t.GetType();
        return new CdpTimingInfo
        {
            RequestTime = (double)type.GetProperty("RequestTime")!.GetValue(t)!,
            DnsStart = (double)type.GetProperty("DnsStart")!.GetValue(t)!,
            DnsEnd = (double)type.GetProperty("DnsEnd")!.GetValue(t)!,
            ConnectStart = (double)type.GetProperty("ConnectStart")!.GetValue(t)!,
            ConnectEnd = (double)type.GetProperty("ConnectEnd")!.GetValue(t)!,
            SslStart = (double)type.GetProperty("SslStart")!.GetValue(t)!,
            SslEnd = (double)type.GetProperty("SslEnd")!.GetValue(t)!,
            SendStart = (double)type.GetProperty("SendStart")!.GetValue(t)!,
            SendEnd = (double)type.GetProperty("SendEnd")!.GetValue(t)!,
            ReceiveHeadersEnd = (double)type.GetProperty("ReceiveHeadersEnd")!.GetValue(t)!
        };
    }

    private static IDictionary<string, string>? CastHeaders(object? headers)
    {
        if (headers == null) return null;
        if (headers is IDictionary<string, string> strDict)
            return strDict;
        if (headers is IDictionary<string, object> objDict)
            return objDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);
        // Last resort: try reflection for any dictionary-like object
        return null;
    }

    private static Type ResolveType(Assembly assembly, string ns, string name)
    {
        return assembly.GetType($"{ns}.{name}")
            ?? throw new InvalidOperationException($"Type {ns}.{name} not found in assembly.");
    }

    /// <summary>
    /// Creates a typed EventHandler&lt;TEventArgs&gt; delegate using expression trees
    /// that forwards events to an Action&lt;object&gt; handler.
    /// </summary>
    private static Delegate CreateTypedHandler(Type eventArgsType, Action<object> handler)
    {
        var senderParam = Expression.Parameter(typeof(object), "sender");
        var argsParam = Expression.Parameter(eventArgsType, "args");
        var body = Expression.Call(
            Expression.Constant(handler),
            typeof(Action<object>).GetMethod("Invoke")!,
            Expression.Convert(argsParam, typeof(object)));
        var handlerType = typeof(EventHandler<>).MakeGenericType(eventArgsType);
        return Expression.Lambda(handlerType, body, senderParam, argsParam).Compile();
    }

    /// <summary>
    /// Subscribes a typed handler to an event and returns the delegate for later unsubscription.
    /// </summary>
    private Delegate SubscribeEvent(EventInfo eventInfo, Action<object> handler)
    {
        var eventArgsType = eventInfo.EventHandlerType!.GetGenericArguments()[0];
        var typedHandler = CreateTypedHandler(eventArgsType, handler);
        eventInfo.AddEventHandler(_network, typedHandler);
        return typedHandler;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _requestWillBeSentEvent.RemoveEventHandler(_network, _requestWillBeSentHandler);
        _responseReceivedEvent.RemoveEventHandler(_network, _responseReceivedHandler);
        _loadingFinishedEvent.RemoveEventHandler(_network, _loadingFinishedHandler);
        _loadingFailedEvent.RemoveEventHandler(_network, _loadingFailedHandler);
    }
}
