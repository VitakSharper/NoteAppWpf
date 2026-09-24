using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NoteApp.UiTests;

// The real views, on a real dispatcher: one STA thread for the whole run, holding the one
// Application a process may have, with the resources App.xaml merges. Tests hand their
// body to Run and it executes there; an exception thrown inside fails the test as usual.
// Nothing here reaches a database — the view models get repositories that throw.
public static class Wpf
{
    private static readonly Lazy<Dispatcher> UiThread = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static void Run(Action body) => UiThread.Value.Invoke(body);

    // Lets deferred work (Background, Loaded, layout) catch up, the way the idle app would.
    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });

    // Shown off-screen: bindings, templates, layout and hit-testing only exist in a live window.
    public static Window Show(FrameworkElement content, double width = 900, double height = 1000)
    {
        var window = new Window
        {
            Content = content,
            Width = width,
            Height = height,
            Left = -10000,
            Top = -10000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false
        };
        window.Show();
        Pump();
        return window;
    }

    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit)
                yield return hit;
            foreach (var deeper in Descendants<T>(child))
                yield return deeper;
        }
    }

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new BundledTheme
            {
                BaseTheme = BaseTheme.Light,
                PrimaryColor = PrimaryColor.DeepPurple,
                SecondaryColor = SecondaryColor.Purple
            });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/NoteApp;component/Theme/ModernViolet.xaml")
            });

            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();

        return dispatcher!;
    }
}
