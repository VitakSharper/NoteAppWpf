using System.Windows;
using System.Windows.Controls;

namespace NoteApp.UiTests;

public class MainWindowUiTests
{
    // The list pane's width is the user's (the splitter): focus mode gives it back unchanged.
    [Fact]
    public void Focus_mode_collapses_the_rail_and_the_list_and_restores_their_widths() => Wpf.Run(() =>
    {
        var window = new MainWindow { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        Wpf.Pump();
        try
        {
            var grid = Wpf.Descendants<Grid>(window).First(g => g.ColumnDefinitions.Count == 4);
            var columns = grid.ColumnDefinitions;
            columns[1].Width = new GridLength(412);

            window.ApplyFocusMode(true);
            Wpf.Pump();
            Assert.Equal([0.0, 0.0, 0.0], columns.Take(3).Select(c => c.ActualWidth));

            window.ApplyFocusMode(false);
            Wpf.Pump();
            Assert.Equal(64, columns[0].ActualWidth);
            Assert.Equal(412, columns[1].Width.Value);
            Assert.Equal(260, columns[1].MinWidth);
        }
        finally
        {
            window.Close();
        }
    });
}
