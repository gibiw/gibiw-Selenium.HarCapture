using System;
using System.IO;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture;

/// <summary>
/// Primary public facade for capturing HAR network traffic from Selenium browser sessions.
/// Wraps <see cref="HarCaptureSession"/> with dual disposal pattern supporting both sync and async cleanup.
/// </summary>
/// <remarks>
/// This is the main class users interact with. It provides:
/// - Sync and async Start/Stop methods for capture lifecycle
/// - GetHar() for snapshots during capture
/// - NewPage() for multi-page captures
/// - IDisposable and IAsyncDisposable for proper resource cleanup
///
/// Example usage:
/// <code>
/// using var capture = new HarCapture(driver);
/// capture.Start("page1", "Home");
/// driver.Navigate().GoToUrl("https://example.com");
/// var har = capture.Stop();
/// </code>
/// </remarks>
public sealed class HarCapture : IDisposable, IAsyncDisposable
{
    private HarCaptureSession? _session;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether capture is currently active.
    /// </summary>
    public bool IsCapturing => _session?.IsCapturing ?? false;

    /// <summary>
    /// Gets the name of the active capture strategy (e.g., "CDP", "INetwork").
    /// Returns null if no strategy is configured or the session has been disposed.
    /// </summary>
    public string? ActiveStrategyName => _session?.ActiveStrategyName;

    /// <summary>
    /// Initializes a new instance with automatic strategy selection based on driver capabilities.
    /// Uses CDP if available, falls back to INetwork if CDP session creation fails.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture network traffic from.</param>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture.</exception>
    public HarCapture(IWebDriver driver, CaptureOptions? options = null)
    {
        if (driver == null)
        {
            throw new ArgumentNullException(nameof(driver));
        }

        _session = new HarCaptureSession(driver, options);
    }

    /// <summary>
    /// Initializes a new instance with a pre-configured session.
    /// Used for testing scenarios via InternalsVisibleTo.
    /// </summary>
    /// <param name="session">The capture session to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when session is null.</exception>
    internal HarCapture(HarCaptureSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Asynchronously starts network traffic capture.
    /// </summary>
    /// <param name="initialPageRef">Optional page reference ID for the initial page. If provided, creates the first page in the HAR.</param>
    /// <param name="initialPageTitle">Optional page title for the initial page. Used only if initialPageRef is provided.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is already started.</exception>
    public async Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null)
    {
        ThrowIfDisposed();
        await _session!.StartAsync(initialPageRef, initialPageTitle).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously starts network traffic capture.
    /// </summary>
    /// <param name="initialPageRef">Optional page reference ID for the initial page. If provided, creates the first page in the HAR.</param>
    /// <param name="initialPageTitle">Optional page title for the initial page. Used only if initialPageRef is provided.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is already started.</exception>
    public void Start(string? initialPageRef = null, string? initialPageTitle = null)
    {
        StartAsync(initialPageRef, initialPageTitle).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously stops network traffic capture and returns the final HAR object.
    /// </summary>
    /// <returns>A task that represents the asynchronous stop operation. The task result contains the final HAR object.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    public async Task<Har> StopAsync()
    {
        ThrowIfDisposed();
        return await _session!.StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously stops network traffic capture and returns the final HAR object.
    /// </summary>
    /// <returns>The final HAR object.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    public Har Stop()
    {
        return StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously stops capture when streaming mode is active (OutputFilePath configured).
    /// The HAR file is already written incrementally; this method completes the file and logs the result.
    /// </summary>
    /// <returns>A task that represents the asynchronous stop-and-save operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when OutputFilePath is not configured or capture is not started.</exception>
    public async Task StopAndSaveAsync()
    {
        ThrowIfDisposed();
        if (_session!.OutputFilePath == null)
            throw new InvalidOperationException(
                "Parameterless StopAndSaveAsync requires OutputFilePath to be configured via WithOutputFile().");

        var logger = _session.Logger;
        logger?.Log("HarCapture", "StopAndSave (streaming): stopping...");
        await _session.StopAsync().ConfigureAwait(false);

        var fileSize = new FileInfo(_session.OutputFilePath).Length;
        logger?.Log("HarCapture", $"StopAndSave (streaming): completed ({fileSize} bytes)");
    }

    /// <summary>
    /// Synchronously stops capture when streaming mode is active (OutputFilePath configured).
    /// The HAR file is already written incrementally; this method completes the file and logs the result.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when OutputFilePath is not configured or capture is not started.</exception>
    public void StopAndSave()
    {
        StopAndSaveAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously stops capture and saves the HAR to a file.
    /// Combines Stop + Save into a single call with diagnostic logging via <see cref="CaptureOptions.LogFilePath"/>.
    /// </summary>
    /// <param name="filePath">The path to the file where the HAR will be saved.</param>
    /// <param name="writeIndented">If true, formats the JSON with indentation for readability. Default is true.</param>
    /// <returns>A task whose result contains the final HAR object.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    public async Task<Har> StopAndSaveAsync(string filePath, bool writeIndented = true)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        // Guard: WithOutputFile + StopAndSaveAsync(path) conflict
        if (_session!.OutputFilePath != null)
        {
            throw new InvalidOperationException(
                $"Cannot call StopAndSaveAsync(path) when WithOutputFile is configured. " +
                $"Output is already streaming to '{_session.OutputFilePath}'. " +
                $"Use parameterless StopAndSaveAsync() instead to complete the streaming file, " +
                $"or remove WithOutputFile() from CaptureOptions to use in-memory capture with explicit save path.");
        }

        var logger = _session.Logger;

        logger?.Log("HarCapture", "StopAndSave: stopping capture...");
        var har = await _session.StopAsync().ConfigureAwait(false);

        logger?.Log("HarCapture", $"StopAndSave: saving {har.Log.Entries?.Count ?? 0} entries to {filePath}");
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                logger?.Log("HarCapture", $"StopAndSave: creating directory {dir}");
                Directory.CreateDirectory(dir);
            }

            await HarSerializer.SaveAsync(har, filePath, writeIndented).ConfigureAwait(false);

            var fileSize = new FileInfo(filePath).Length;
            logger?.Log("HarCapture", $"StopAndSave: saved successfully ({fileSize} bytes)");
        }
        catch (Exception ex)
        {
            logger?.Log("HarCapture", $"StopAndSave: save failed: {ex}");
            throw;
        }

        return har;
    }

    /// <summary>
    /// Synchronously stops capture and saves the HAR to a file.
    /// Combines Stop + Save into a single call with diagnostic logging via <see cref="CaptureOptions.LogFilePath"/>.
    /// </summary>
    /// <param name="filePath">The path to the file where the HAR will be saved.</param>
    /// <param name="writeIndented">If true, formats the JSON with indentation for readability. Default is true.</param>
    /// <returns>The final HAR object.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    public Har StopAndSave(string filePath, bool writeIndented = true)
    {
        return StopAndSaveAsync(filePath, writeIndented).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets a deep-cloned snapshot of the current HAR data while capture continues.
    /// </summary>
    /// <returns>A deep-cloned HAR object that is independent of the live capture.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    /// <remarks>
    /// The returned HAR is a complete copy. Modifying it will not affect the ongoing capture.
    /// Uses JSON round-trip serialization for deep cloning to ensure thread safety.
    /// </remarks>
    public Har GetHar()
    {
        ThrowIfDisposed();
        return _session!.GetHar();
    }

    /// <summary>
    /// Creates a new page in the HAR capture.
    /// Subsequent entries will be associated with this page via their PageRef property.
    /// </summary>
    /// <param name="pageRef">Unique identifier for the page. This will be used in entry PageRef fields.</param>
    /// <param name="pageTitle">Human-readable title for the page.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the capture has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    /// <exception cref="ArgumentNullException">Thrown when pageRef or pageTitle is null.</exception>
    public void NewPage(string pageRef, string pageTitle)
    {
        ThrowIfDisposed();
        _session!.NewPage(pageRef, pageTitle);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="HarCapture"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases all resources used by the <see cref="HarCapture"/>.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _session?.Dispose();
            _session = null;
        }

        _disposed = true;
    }

    /// <summary>
    /// Performs async cleanup of resources.
    /// </summary>
    /// <returns>A task that represents the async cleanup operation.</returns>
    private async ValueTask DisposeAsyncCore()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the instance has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCapture));
        }
    }
}
