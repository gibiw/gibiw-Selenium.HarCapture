using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Selenium.HarCapture.IntegrationTests.Infrastructure;

public sealed class TestWebServer : IAsyncLifetime
{
    private WebApplication? _app;

    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        MapEndpoints(_app);

        await _app.StartAsync();

        var address = _app.Urls.First();
        BaseUrl = address;
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Content(
            """
            <!DOCTYPE html>
            <html><head><title>Test Page</title></head>
            <body><h1>Hello Integration Tests</h1></body></html>
            """,
            "text/html"));

        app.MapGet("/api/data", () => Results.Json(new { message = "hello", value = 42 }));

        app.MapGet("/api/large", () =>
        {
            var text = new string('A', 10 * 1024);
            return Results.Text(text, "text/plain");
        });

        app.MapGet("/api/cookies", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Append("test-cookie", "cookie-value");
            return Results.Json(new { cookies = "set" });
        });

        app.MapGet("/with-fetch", () => Results.Content(
            """
            <!DOCTYPE html>
            <html><head><title>Fetch Page</title></head>
            <body>
            <div id="result"></div>
            <script>
            fetch('/api/data')
              .then(r => r.json())
              .then(d => document.getElementById('result').textContent = JSON.stringify(d));
            </script>
            </body></html>
            """,
            "text/html"));

        app.MapGet("/page2", () => Results.Content(
            """
            <!DOCTYPE html>
            <html><head><title>Page Two</title></head>
            <body><h1>Page 2</h1></body></html>
            """,
            "text/html"));

        app.MapGet("/redirect", () => Results.Redirect("/api/data"));

        app.MapGet("/api/slow", async () =>
        {
            await Task.Delay(200);
            return Results.Json(new { slow = true });
        });
    }
}
