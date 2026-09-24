using System.ComponentModel;
using System.Diagnostics;
using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Services;

// Opens a link in the default browser. It takes a LinkUrl, not a string or a Uri: the
// shell runs whatever it is handed, and a LinkUrl can only be an HTTP or HTTPS address.
public static class LinkLauncher
{
    public static Result<Unit, AppError> Open(LinkUrl url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url.Value.AbsoluteUri) { UseShellExecute = true });
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Win32Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Io($"Could not open {url}: {ex.Message}"));
        }
    }
}
