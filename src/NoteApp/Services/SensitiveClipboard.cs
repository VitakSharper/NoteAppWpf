using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace NoteApp.Services;

// What NoteApp copies out of a secret block or an encrypted note. Windows is told to keep
// it out of the clipboard history (Win+V) and the cloud clipboard, and it is wiped after
// ClearAfter — unless something else has been copied since, which is then left alone.
// "Still ours" is the clipboard sequence number, which Windows bumps on every change.
public static class SensitiveClipboard
{
    public static readonly TimeSpan ClearAfter = TimeSpan.FromSeconds(30);

    // The registered formats Windows looks for ("Cloud Clipboard and Clipboard History
    // Formats" in the Win32 clipboard documentation): any data in the first keeps the
    // item out of both, a DWORD 0 in each of the other two opts out of one of them.
    internal static readonly IReadOnlyList<string> PrivateFormats =
        ["ExcludeClipboardContentFromMonitorProcessing", "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard"];

    private static DispatcherTimer? _timer;
    private static uint _ours;

    // A MemoryStream goes onto the clipboard as its raw bytes: the DWORD 0 Windows reads.
    public static void MarkPrivate(IDataObject data)
    {
        foreach (var format in PrivateFormats)
            data.SetData(format, new MemoryStream(BitConverter.GetBytes(0)));
    }

    // The copy buttons of a secret block. false when another program holds the clipboard.
    public static bool Copy(string text)
    {
        var data = new DataObject(DataFormats.UnicodeText, text);
        MarkPrivate(data);
        try
        {
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (ExternalException)
        {
            return false;
        }

        ClearLater();
        return true;
    }

    // Once the copy has landed (a Ctrl+C in an encrypted note calls it after the fact).
    public static void ClearLater()
    {
        _ours = GetClipboardSequenceNumber();
        _timer ??= NewTimer();
        _timer.Stop();
        _timer.Start();
    }

    // Also on exit: nothing copied from a secret outlives the app on the clipboard.
    public static void ClearIfStillOurs()
    {
        _timer?.Stop();
        var ours = _ours;
        _ours = 0;
        if (!IsStillOurs(ours, GetClipboardSequenceNumber()))
            return;

        try
        {
            Clipboard.Clear();
        }
        catch (ExternalException)
        {
            // Another program has it open; there is no retrying a wipe forever.
        }
    }

    internal static bool IsStillOurs(uint ours, uint current) => ours != 0 && ours == current;

    private static DispatcherTimer NewTimer()
    {
        var timer = new DispatcherTimer { Interval = ClearAfter };
        timer.Tick += (_, _) => ClearIfStillOurs();
        return timer;
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
