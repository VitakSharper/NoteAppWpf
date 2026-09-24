using System.IO;
using System.Windows;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

// The real clipboard is the user's: these tests only look at what would be put on it.
public class SensitiveClipboardTests
{
    [Fact]
    public void A_private_copy_carries_the_formats_that_keep_it_out_of_history_and_the_cloud()
    {
        var data = new DataObject(DataFormats.UnicodeText, "hunter2");

        SensitiveClipboard.MarkPrivate(data);

        Assert.Equal(
            ["ExcludeClipboardContentFromMonitorProcessing", "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard"],
            SensitiveClipboard.PrivateFormats);
        foreach (var format in SensitiveClipboard.PrivateFormats)
        {
            var stream = Assert.IsType<MemoryStream>(data.GetData(format));
            Assert.Equal(new byte[4], stream.ToArray()); // a DWORD 0: "do not include"
        }
        Assert.Equal("hunter2", data.GetData(DataFormats.UnicodeText));
    }

    // Wiped only when nothing was copied since: the sequence number moves on every change.
    [Theory]
    [InlineData(41u, 41u, true)]
    [InlineData(41u, 42u, false)]
    [InlineData(0u, 0u, false)]
    public void The_clipboard_is_wiped_only_while_it_still_holds_our_copy(uint ours, uint current, bool wiped) =>
        Assert.Equal(wiped, SensitiveClipboard.IsStillOurs(ours, current));
}
