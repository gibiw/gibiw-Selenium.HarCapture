using System;
using System.Collections.Generic;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Tests.Fixtures;

/// <summary>
/// Factory for creating test HarEntry instances.
/// </summary>
internal static class HarEntryFactory
{
    /// <summary>
    /// Creates a minimal valid HarEntry for testing purposes.
    /// </summary>
    internal static HarEntry CreateTestEntry(string url = "https://example.com/page")
    {
        return new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 100,
            Request = new HarRequest
            {
                Method = "GET",
                Url = url,
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = -1
            },
            Response = new HarResponse
            {
                Status = 200,
                StatusText = "OK",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                Content = new HarContent
                {
                    Size = 0,
                    MimeType = "text/html"
                },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = -1
            },
            Cache = new HarCache(),
            Timings = new HarTimings
            {
                Send = 1,
                Wait = 50,
                Receive = 49
            }
        };
    }
}
