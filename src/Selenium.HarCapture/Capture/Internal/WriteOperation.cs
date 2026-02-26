using System.Threading.Tasks;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

internal readonly struct WriteOperation
{
    public enum OpType { Entry, Page, Flush }
    public OpType Type { get; }
    public HarEntry? Entry { get; }
    public HarPage? Page { get; }
    public TaskCompletionSource<bool>? FlushTcs { get; }

    private WriteOperation(OpType type, HarEntry? entry = null, HarPage? page = null, TaskCompletionSource<bool>? flushTcs = null)
    {
        Type = type;
        Entry = entry;
        Page = page;
        FlushTcs = flushTcs;
    }

    public static WriteOperation CreateEntry(HarEntry entry) =>
        new WriteOperation(OpType.Entry, entry: entry);

    public static WriteOperation CreatePage(HarPage page) =>
        new WriteOperation(OpType.Page, page: page);

    public static WriteOperation CreateFlush(TaskCompletionSource<bool> tcs) =>
        new WriteOperation(OpType.Flush, flushTcs: tcs);
}
