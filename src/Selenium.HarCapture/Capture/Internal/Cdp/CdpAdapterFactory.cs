using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenQA.Selenium.DevTools;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Creates the appropriate CDP Network adapter by auto-discovering available CDP versions
/// via assembly scanning. Tries newest version first, falls back to raw CDP commands
/// if no compatible version-specific adapter can be created.
/// </summary>
internal static class CdpAdapterFactory
{
    /// <summary>
    /// Creates an <see cref="ICdpNetworkAdapter"/> for the given DevTools session.
    /// First scans the Selenium assembly for V{N}.DevToolsSessionDomains types
    /// and tries them from newest to oldest. If all fail, falls back to a raw
    /// CDP command adapter that is version-agnostic.
    /// </summary>
    /// <param name="session">An active DevTools session.</param>
    /// <param name="logger">Optional file logger for diagnostics.</param>
    /// <returns>A configured adapter that implements <see cref="ICdpNetworkAdapter"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no adapter can be created (neither reflection-based nor raw).
    /// </exception>
    internal static ICdpNetworkAdapter Create(DevToolsSession session, FileLogger? logger = null)
    {
        // Try reflection-based adapter first (uses version-specific generated types)
        var reflectiveAdapter = TryCreateReflective(session, logger);
        if (reflectiveAdapter != null)
        {
            return reflectiveAdapter;
        }

        // Fallback: raw CDP commands via DevToolsSession.SendCommand(string, JsonNode)
        logger?.Log("CDP", "All version-specific adapters failed, falling back to raw CDP commands (version-agnostic)");
        try
        {
            var rawAdapter = new RawCdpNetworkAdapter(session, logger);
            logger?.Log("CDP", "Raw CDP adapter created successfully");
            return rawAdapter;
        }
        catch (Exception ex)
        {
            logger?.Log("CDP", $"Raw CDP adapter creation failed: {ex.Message}");
            throw new InvalidOperationException(
                "No compatible CDP adapter could be created. " +
                "Neither version-specific reflection nor raw CDP commands worked. " +
                $"Raw adapter error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tries to create a reflection-based adapter by scanning for version-specific CDP types.
    /// Returns null if no compatible version is found.
    /// </summary>
    private static ICdpNetworkAdapter? TryCreateReflective(DevToolsSession session, FileLogger? logger)
    {
        var assembly = typeof(DevToolsSession).Assembly;

        var versionTypes = assembly.GetTypes()
            .Select(t => (Type: t, Match: Regex.Match(t.FullName ?? "", @"\.V(\d+)\.DevToolsSessionDomains$")))
            .Where(x => x.Match.Success)
            .Select(x => (x.Type, Version: int.Parse(x.Match.Groups[1].Value)))
            .OrderByDescending(x => x.Version)
            .ToList();

        logger?.Log("CDP", $"Found CDP versions in assembly: {string.Join(", ", versionTypes.Select(x => $"V{x.Version}"))}");

        foreach (var (domainsType, version) in versionTypes)
        {
            try
            {
                logger?.Log("CDP", $"Trying V{version}: {domainsType.FullName}");
                var adapter = new ReflectiveCdpNetworkAdapter(session, domainsType);
                logger?.Log("CDP", $"V{version}: adapter created successfully");
                return adapter;
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                logger?.Log("CDP", $"V{version}: failed (TargetInvocationException): {inner}");
            }
            catch (InvalidOperationException ex)
            {
                logger?.Log("CDP", $"V{version}: failed (InvalidOperationException): {ex.Message}");
            }
            catch (Exception ex)
            {
                logger?.Log("CDP", $"V{version}: failed ({ex.GetType().Name}): {ex.Message}");
            }
        }

        return null;
    }
}
