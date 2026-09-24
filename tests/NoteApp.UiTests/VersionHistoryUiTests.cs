using System.Text;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;
using NoteApp.Views;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class VersionHistoryUiTests
{
    [Fact]
    public void The_history_lists_the_versions_shows_one_and_restores_it() => Wpf.Run(() =>
    {
        var editor = new NoteEditorViewModel(new NoteService(new TwoVersions()), new UnusedTagRepository(), [], With(Text("now")));
        var window = new VersionHistoryWindow(editor) { Left = -10000, Top = -10000, ShowActivated = false };
        window.Show();
        try
        {
            window.LoadAsync().GetAwaiter().GetResult();
            Wpf.Pump();

            var rows = window.Versions.Items.Cast<VersionHistoryWindow.Row>().ToList();
            Assert.Equal(["Monday", "Sunday"], rows.Select(r => r.Version.Title));
            window.ShowSelectedAsync().GetAwaiter().GetResult();
            Assert.Equal(["☐ agenda"], window.PreviewLines.Items.Cast<string>());
            Assert.True(window.RestoreButton.IsEnabled);

            editor.RestoreVersion((editor.OpenVersionAsync(rows[0].Version.Id).GetAwaiter().GetResult()).Match(c => c, e => throw new InvalidOperationException(e.Message)));
            Assert.Equal("Monday", editor.Title);
            Assert.True(editor.IsDirty);
        }
        finally
        {
            window.Close();
        }
    });

    private sealed class TwoVersions : UnusedNoteRepository
    {
        private static readonly Guid Monday = Guid.NewGuid(), Sunday = Guid.NewGuid();

        public override Task<Result<IReadOnlyList<NoteVersionRow>, AppError>> VersionsAsync(NoteId id) =>
            Task.FromResult(Result<IReadOnlyList<NoteVersionRow>, AppError>.Ok(
            [
                new NoteVersionRow { Id = Monday, Title = "Monday", SavedAt = DateTime.UtcNow.AddDays(-1), SizeBytes = 120 },
                new NoteVersionRow { Id = Sunday, Title = "Sunday", SavedAt = DateTime.UtcNow.AddDays(-2), SizeBytes = 80 }
            ]));

        public override Task<Result<NoteVersionRow, AppError>> GetVersionAsync(Guid versionId) =>
            Task.FromResult(Result<NoteVersionRow, AppError>.Ok(new NoteVersionRow
            {
                Id = versionId, Title = versionId == Monday ? "Monday" : "Sunday", SavedAt = DateTime.UtcNow,
                Content = Encoding.UTF8.GetBytes(EncryptionService.BlocksToJson([Checklist("agenda")]))
            }));
    }
}
