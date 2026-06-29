# NoteApp UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign NoteApp's UI into a modern three-pane workspace (icon rail · note list · editor) with a "Modern Violet" Material Design 3 theme, a Comfortable note list, Mixed rail navigation (Tags swaps the middle pane, Settings is a modal), and dark mode.

**Architecture:** Styling/layout plus a `MainViewModel` navigation rework. The single `CurrentView` becomes two surfaces — `MiddlePaneContent` (NoteList ↔ TagManager) and `CurrentEditor` (editor or empty-state) — with Settings shown via the existing `DialogHost`. Leaf ViewModels keep their public surfaces; services, repositories, domain models, and EF are untouched.

**Tech Stack:** WPF (.NET 10), MaterialDesignThemes 5.2.1 (MD3), CommunityToolkit.Mvvm 8.4.1.

## Global Constraints

- **No automated test harness exists** and UI styling is not unit-testable. Every task's verification = **build succeeds + run the app + observe the stated behavior**, then commit. Do **not** create a test project; it is out of scope.
- **Build:** `dotnet build NoteApp.slnx` — must succeed with no new warnings about missing resources/bindings.
- **Run:** `dotnet run --project src/NoteApp` (requires the `ConnectionStrings:NoteDb` user secret already configured; if the DB is unreachable, note it and verify what you can — the window shell still renders).
- **Do not touch:** `EncryptionService`, `BackupService`, repositories, `NoteService`, `Domain/**`, `Data/**`, EF configs/migrations, `NoteMapper`. No schema change, no EF migration.
- **Branch:** work happens on `ui-redesign` (already created; spec committed at `7674bd8`).
- **Palette (exact values):** accent `#6C79FF`, rail navy `#20283D`, rail foreground `#E8EAF2`, rail selected indicator `#6C79FF`. Tag chip (violet): bg `#E6E8FF`, fg `#3A3F8F`, border `#C5CAF5`, selected bg `#6C79FF`, selected fg `#FFFFFF`.
- **Commit style:** prefix `feat:` / `style:` / `refactor:`; end body with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `src/NoteApp/Theme/ModernViolet.xaml` | Custom brushes (rail, accent, surfaces, tag chips) + corner-radius tokens | **Create** |
| `src/NoteApp/App.xaml` | MD3 defaults + violet bundled theme + merge ModernViolet.xaml | Modify |
| `src/NoteApp/ViewModels/SettingsViewModel.cs` | `ApplyTheme` → violet (light + dark) | Modify (`ApplyTheme` only) |
| `src/NoteApp/MainWindow.xaml` | Three-pane shell + icon rail + Settings DialogHost | Rewrite layout |
| `src/NoteApp/ViewModels/MainViewModel.cs` | `MiddlePaneContent` + `CurrentEditor` + Settings dialog orchestration | Rework navigation |
| `src/NoteApp/Converters/Converters.cs` | Add `NotePreviewConverter`, `NullToVisibilityConverter` | Modify (append) |
| `src/NoteApp/Views/NoteListView.xaml` | Comfortable single-column list; remove card/grid toggle | Rewrite list region |
| `src/NoteApp/ViewModels/NoteListViewModel.cs` | Remove `IsListView`/`ToggleViewMode`; open-on-select | Modify |
| `src/NoteApp/Views/NoteEditorView.xaml` | MD3/violet restyle (right pane) | Restyle |
| `src/NoteApp/Views/TagManagerView.xaml` | MD3/violet restyle (middle pane) | Restyle |
| `src/NoteApp/Views/PasswordDialog.xaml` | MD3/violet restyle | Restyle |

---

## Task 1: Theme foundation — MD3 + Modern Violet

**Files:**
- Create: `src/NoteApp/Theme/ModernViolet.xaml`
- Modify: `src/NoteApp/App.xaml`
- Modify: `src/NoteApp/ViewModels/SettingsViewModel.cs:133-153` (`ApplyTheme`)

**Interfaces:**
- Produces: resource keys consumed by later tasks — `RailBackgroundBrush`, `RailForegroundBrush`, `RailSelectedBrush`, `AccentBrush`, `TagChipBackground`, `TagChipForeground`, `TagChipBorder`, `TagChipSelectedBackground`, `TagChipSelectedForeground`, `CardCornerRadius` (CornerRadius), `ControlCornerRadius` (CornerRadius).

- [ ] **Step 1: Create the Modern Violet resource dictionary**

Create `src/NoteApp/Theme/ModernViolet.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">

    <!-- Rail chrome (constant across light/dark) -->
    <SolidColorBrush x:Key="RailBackgroundBrush" Color="#20283D" />
    <SolidColorBrush x:Key="RailForegroundBrush" Color="#E8EAF2" />
    <SolidColorBrush x:Key="RailSelectedBrush" Color="#6C79FF" />

    <!-- Accent -->
    <SolidColorBrush x:Key="AccentBrush" Color="#6C79FF" />

    <!-- Tag chips (violet) -->
    <SolidColorBrush x:Key="TagChipBackground" Color="#E6E8FF" />
    <SolidColorBrush x:Key="TagChipForeground" Color="#3A3F8F" />
    <SolidColorBrush x:Key="TagChipBorder" Color="#C5CAF5" />
    <SolidColorBrush x:Key="TagChipSelectedBackground" Color="#6C79FF" />
    <SolidColorBrush x:Key="TagChipSelectedForeground" Color="#FFFFFF" />

    <!-- Corner radius tokens (MD3 soft surfaces) -->
    <CornerRadius x:Key="CardCornerRadius">12</CornerRadius>
    <CornerRadius x:Key="ControlCornerRadius">8</CornerRadius>
</ResourceDictionary>
```

- [ ] **Step 2: Point App.xaml at MD3 + violet + merge ModernViolet.xaml**

Replace the `<ResourceDictionary.MergedDictionaries>` block in `src/NoteApp/App.xaml` (lines 8-11) with:

```xml
<ResourceDictionary.MergedDictionaries>
    <materialDesign:BundledTheme BaseTheme="Light" PrimaryColor="DeepPurple" SecondaryColor="Purple" ColorAdjustment="{materialDesign:ColorAdjustment}" />
    <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml" />
    <ResourceDictionary Source="Theme/ModernViolet.xaml" />
</ResourceDictionary.MergedDictionaries>
```

(`BundledTheme` only accepts named `PrimaryColor` swatches; `DeepPurple`/`Purple` are the closest swatches and the exact `#6C79FF` accent is applied at runtime in Step 3 via `ApplyTheme`. `ColorAdjustment` improves contrast under MD3.)

- [ ] **Step 3: Update ApplyTheme to the violet accent (light + dark)**

Replace the body of `ApplyTheme` in `src/NoteApp/ViewModels/SettingsViewModel.cs` (lines 133-153) with:

```csharp
    public static void ApplyTheme(bool isDark)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

        // Modern Violet accent
        var violet = Color.FromRgb(0x6C, 0x79, 0xFF);
        var violetLight = Color.FromRgb(0x9A, 0xA3, 0xFF); // lighter for dark-mode contrast
        theme.SetPrimaryColor(isDark ? violetLight : violet);
        theme.SetSecondaryColor(isDark ? violetLight : violet);

        paletteHelper.SetTheme(theme);
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build NoteApp.slnx`
Expected: Build succeeds. If it fails because `MaterialDesign3.Defaults.xaml` is not found, see the GO/NO-GO step below.

- [ ] **Step 5: Run and verify (GO/NO-GO checkpoint)**

Run: `dotnet run --project src/NoteApp`
Expected: The existing app launches and renders without missing-resource/crash. Accent color shifts toward violet.

**GO/NO-GO:** If MD3 causes widespread broken control styling (buttons/cards/text rendering wrong or `XamlParseException` on known style keys like `MaterialDesignWindow`, `MaterialDesignFlatButton`, `MaterialDesignCard`), **fall back to MD2**: revert the defaults line to `MaterialDesign2.Defaults.xaml`, keep `ModernViolet.xaml` + the violet `ApplyTheme`, and achieve "rounded/soft" purely via the `CardCornerRadius`/`ControlCornerRadius` tokens and `materialDesign:ButtonAssist.CornerRadius` in later tasks. Record the choice in the commit message. The rest of the plan works under either base.

- [ ] **Step 6: Commit**

```bash
git add src/NoteApp/Theme/ModernViolet.xaml src/NoteApp/App.xaml src/NoteApp/ViewModels/SettingsViewModel.cs
git commit -m "$(cat <<'EOF'
style: add Modern Violet theme foundation (MD3 + violet accent)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Three-pane shell + MainViewModel navigation rework

This task delivers a working three-pane app reusing the **existing** `NoteListView`, `TagManagerView`, `SettingsView`, `NoteEditorView` (restyled later). The shell and the VM change together because the shell's bindings depend on the new VM properties.

**Files:**
- Modify: `src/NoteApp/ViewModels/MainViewModel.cs` (replace `CurrentView` with `MiddlePaneContent` + `CurrentEditor` + Settings dialog)
- Modify: `src/NoteApp/Converters/Converters.cs` (append `NullToVisibilityConverter`)
- Rewrite layout: `src/NoteApp/MainWindow.xaml`

**Interfaces:**
- Produces (MainViewModel): `ObservableObject? MiddlePaneContent`, `ObservableObject? CurrentEditor`, `bool IsSettingsOpen`, commands `NavigateToNotesCommand`, `NavigateToTagsCommand`, `OpenSettingsCommand`, `CreateNoteCommand`.
- Consumes: `SettingsViewModel.CloseRequested`, `NoteListViewModel.EditNoteRequested/CreateNoteRequested`, `NoteEditorViewModel.SaveCompleted/CancelRequested` (unchanged signatures).

- [ ] **Step 1: Append `NullToVisibilityConverter` to Converters.cs**

Add at the end of `src/NoteApp/Converters/Converters.cs` (before nothing else needed):

```csharp
public sealed class NullToVisibilityConverter : System.Windows.Data.IValueConverter
{
    // value == null  -> Visible (show placeholder); else Collapsed
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Rework MainViewModel navigation**

Replace the contents of `src/NoteApp/ViewModels/MainViewModel.cs` with:

```csharp
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.Views;

namespace NoteApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly ITagRepository _tagRepository;
    private readonly AppSettingsService _settingsService;

    // Middle pane: NoteList <-> TagManager
    [ObservableProperty] private ObservableObject? _middlePaneContent;
    // Right pane: a NoteEditorViewModel, or null => empty-state placeholder
    [ObservableProperty] private ObservableObject? _currentEditor;
    // Settings modal (hosted in RootDialog)
    [ObservableProperty] private bool _isSettingsOpen;

    public NoteListViewModel NoteListViewModel { get; }
    public TagManagerViewModel TagManagerViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(3));

    public MainViewModel(
        NoteService noteService,
        AppSettingsService settingsService,
        ITagRepository tagRepository,
        NoteListViewModel noteListViewModel,
        TagManagerViewModel tagManagerViewModel,
        SettingsViewModel settingsViewModel)
    {
        _noteService = noteService;
        _settingsService = settingsService;
        _tagRepository = tagRepository;
        NoteListViewModel = noteListViewModel;
        TagManagerViewModel = tagManagerViewModel;
        SettingsViewModel = settingsViewModel;

        noteListViewModel.EditNoteRequested += OnEditNoteRequested;
        noteListViewModel.CreateNoteRequested += OnCreateNoteRequested;
        noteListViewModel.ShowMessage += OnShowMessage;

        tagManagerViewModel.ShowMessage += OnShowMessage;
        settingsViewModel.ShowMessage += OnShowMessage;
        settingsViewModel.CloseRequested += () => IsSettingsOpen = false;

        // Startup: choose middle pane; Settings.LaunchPage opens the dialog over Notes
        MiddlePaneContent = _settingsService.Current.LaunchPage == StartupPage.Tags
            ? TagManagerViewModel
            : NoteListViewModel;
        if (_settingsService.Current.LaunchPage == StartupPage.Settings)
            IsSettingsOpen = true;
    }

    [RelayCommand]
    private void NavigateToNotes() => MiddlePaneContent = NoteListViewModel;

    [RelayCommand]
    private void NavigateToTags()
    {
        MiddlePaneContent = TagManagerViewModel;
        TagManagerViewModel.LoadTagsCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private async Task CreateNote()
    {
        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentEditor = editor;
    }

    private async void OnEditNoteRequested(Note note)
    {
        string? password = null;

        if (note.IsEncrypted)
        {
            var dialog = new PasswordDialog(isSetMode: false)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true) return;
            password = dialog.Password;

            var decryptResult = await _noteService.UnlockNoteAsync(note.Id, password);
            if (decryptResult.IsFailure)
            {
                var error = ((Result<Note, AppError>.Failure)decryptResult).Error;
                MessageQueue.Enqueue(error.Message);
                return;
            }
            note = ((Result<Note, AppError>.Success)decryptResult).Value;
        }

        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentEditor = editor;
    }

    private void OnCreateNoteRequested() => CreateNoteCommand.Execute(null);

    private async void OnNoteSaved(Note note, string? password)
    {
        MessageQueue.Enqueue($"Note '{note.Title}' saved successfully.");
        NoteListViewModel.LoadNotesCommand.Execute(null);

        if (CurrentEditor is NoteEditorViewModel existingEditor)
        {
            existingEditor.RefreshAfterSave(note);
        }
        else
        {
            var tags = await _tagRepository.GetAllAsync();
            var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

            var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password);
            editor.SaveCompleted += OnNoteSaved;
            editor.CancelRequested += OnEditorCancelled;
            CurrentEditor = editor;
        }
    }

    // Returning to empty-state leaves the note list intact in the middle pane
    private void OnEditorCancelled() => CurrentEditor = null;

    private void OnShowMessage(string message) => MessageQueue.Enqueue(message);
}
```

- [ ] **Step 3: Rewrite MainWindow.xaml as a three-pane shell**

Replace the entire contents of `src/NoteApp/MainWindow.xaml` with:

```xml
<Window x:Class="NoteApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
        xmlns:vm="clr-namespace:NoteApp.ViewModels"
        xmlns:views="clr-namespace:NoteApp.Views"
        xmlns:converters="clr-namespace:NoteApp.Converters"
        mc:Ignorable="d"
        Title="NoteApp" Height="720" Width="1180" MinHeight="560" MinWidth="980"
        WindowStartupLocation="CenterScreen"
        Style="{StaticResource MaterialDesignWindow}">

    <Window.Resources>
        <converters:NullToVisibilityConverter x:Key="NullToVis" />
        <DataTemplate DataType="{x:Type vm:NoteListViewModel}">
            <views:NoteListView />
        </DataTemplate>
        <DataTemplate DataType="{x:Type vm:NoteEditorViewModel}">
            <views:NoteEditorView />
        </DataTemplate>
        <DataTemplate DataType="{x:Type vm:TagManagerViewModel}">
            <views:TagManagerView />
        </DataTemplate>
    </Window.Resources>

    <materialDesign:DialogHost Identifier="RootDialog"
                               IsOpen="{Binding IsSettingsOpen}"
                               CloseOnClickAway="False">
        <materialDesign:DialogHost.DialogContent>
            <Grid Width="560" MaxHeight="640">
                <views:SettingsView DataContext="{Binding DataContext.SettingsViewModel,
                                     RelativeSource={RelativeSource AncestorType=materialDesign:DialogHost}}" />
            </Grid>
        </materialDesign:DialogHost.DialogContent>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="64" />                          <!-- icon rail -->
                <ColumnDefinition Width="340" MinWidth="260" />          <!-- middle pane -->
                <ColumnDefinition Width="Auto" />                        <!-- splitter -->
                <ColumnDefinition Width="*" MinWidth="360" />            <!-- editor pane -->
            </Grid.ColumnDefinitions>

            <!-- Icon rail -->
            <Border Grid.Column="0" Background="{StaticResource RailBackgroundBrush}">
                <DockPanel LastChildFill="False">
                    <!-- App glyph -->
                    <Viewbox DockPanel.Dock="Top" Width="30" Height="30" Margin="0,16,0,20">
                        <Canvas Width="24" Height="24">
                            <Path Data="M3,1 L15,1 L21,7 L21,23 L3,23 Z" Fill="White"/>
                            <Path Data="M15,1 L15,7 L21,7 Z" Fill="White" Opacity="0.45"/>
                            <Path Data="M19.5,15 L22.5,12 L23.5,13 L20.5,16 Z" Fill="#FFC107"/>
                        </Canvas>
                    </Viewbox>

                    <Button DockPanel.Dock="Top" Style="{StaticResource MaterialDesignIconButton}"
                            Foreground="{StaticResource RailForegroundBrush}" Height="48"
                            Command="{Binding NavigateToNotesCommand}" ToolTip="Notes">
                        <materialDesign:PackIcon Kind="NoteMultiple" Width="24" Height="24" />
                    </Button>
                    <Button DockPanel.Dock="Top" Style="{StaticResource MaterialDesignIconButton}"
                            Foreground="{StaticResource RailForegroundBrush}" Height="48"
                            Command="{Binding NavigateToTagsCommand}" ToolTip="Tags">
                        <materialDesign:PackIcon Kind="TagMultiple" Width="24" Height="24" />
                    </Button>
                    <Button DockPanel.Dock="Top" Style="{StaticResource MaterialDesignIconButton}"
                            Foreground="{StaticResource RailForegroundBrush}" Height="48"
                            Command="{Binding CreateNoteCommand}" ToolTip="New note">
                        <materialDesign:PackIcon Kind="Plus" Width="24" Height="24" />
                    </Button>

                    <!-- Settings pinned at the bottom -->
                    <Button DockPanel.Dock="Bottom" Style="{StaticResource MaterialDesignIconButton}"
                            Foreground="{StaticResource RailForegroundBrush}" Height="48" Margin="0,0,0,12"
                            Command="{Binding OpenSettingsCommand}" ToolTip="Settings">
                        <materialDesign:PackIcon Kind="CogOutline" Width="24" Height="24" />
                    </Button>
                </DockPanel>
            </Border>

            <!-- Middle pane -->
            <Grid Grid.Column="1" Background="{DynamicResource MaterialDesignPaper}">
                <ContentControl Content="{Binding MiddlePaneContent}" />
                <materialDesign:Snackbar VerticalAlignment="Bottom"
                                         MessageQueue="{Binding MessageQueue}" />
            </Grid>

            <GridSplitter Grid.Column="2" Width="4" HorizontalAlignment="Center"
                          VerticalAlignment="Stretch" Background="Transparent"
                          ResizeBehavior="PreviousAndNext" ResizeDirection="Columns" />

            <!-- Editor pane -->
            <Grid Grid.Column="3" Background="{DynamicResource MaterialDesignCardBackground}">
                <ContentControl Content="{Binding CurrentEditor}" />
                <!-- Empty-state placeholder shown when no editor is open -->
                <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                            Visibility="{Binding CurrentEditor, Converter={StaticResource NullToVis}}">
                    <materialDesign:PackIcon Kind="NoteEditOutline" Width="72" Height="72"
                                             HorizontalAlignment="Center"
                                             Foreground="{DynamicResource MaterialDesignBodyLight}" />
                    <TextBlock Text="Select a note, or create one"
                               Margin="0,12,0,0" HorizontalAlignment="Center"
                               Foreground="{DynamicResource MaterialDesignBodyLight}"
                               FontSize="15" />
                    <Button Content="New note" Margin="0,16,0,0" HorizontalAlignment="Center"
                            Style="{StaticResource MaterialDesignRaisedButton}"
                            Command="{Binding CreateNoteCommand}" />
                </StackPanel>
            </Grid>
        </Grid>
    </materialDesign:DialogHost>
</Window>
```

- [ ] **Step 4: Build**

Run: `dotnet build NoteApp.slnx`
Expected: Build succeeds. (If `XamlParseException` references `CurrentView`, the old MainViewModel binding leaked — confirm Step 2 fully replaced the file.)

- [ ] **Step 5: Run and verify three-pane navigation**

Run: `dotnet run --project src/NoteApp`
Verify:
- Window shows the navy icon rail, the note list in the middle pane, and the empty-state ("Select a note, or create one") in the right pane.
- Clicking **Tags** in the rail swaps the middle pane to the tag manager; clicking **Notes** swaps it back.
- Clicking **+** (rail) or "New note" opens the editor in the right pane; **Cancel** returns to the empty-state with the list still present.
- Clicking **Settings** (rail bottom) opens the Settings modal over everything; closing it returns to the panes.
- Opening an existing note populates the right pane; encrypted note still prompts for a password.

- [ ] **Step 6: Commit**

```bash
git add src/NoteApp/MainWindow.xaml src/NoteApp/ViewModels/MainViewModel.cs src/NoteApp/Converters/Converters.cs
git commit -m "$(cat <<'EOF'
feat: three-pane shell with rail nav, editor pane, settings modal

Replaces DrawerHost + single CurrentView with MiddlePaneContent +
CurrentEditor; Settings now opens in RootDialog.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Comfortable note list + preview snippet

**Files:**
- Modify: `src/NoteApp/Converters/Converters.cs` (append `NotePreviewConverter`)
- Modify: `src/NoteApp/ViewModels/NoteListViewModel.cs` (remove `IsListView`/`ToggleViewMode`; open-on-select)
- Rewrite list region: `src/NoteApp/Views/NoteListView.xaml`

**Interfaces:**
- Consumes: `Note` (`Title.Value`, `Blocks`, `Tags`, `IsEncrypted`, `UpdatedAt`, `HasText`, `HasFiles`, `HasLinks`), `NoteBlock.Text.RichText`.
- Produces: `NotePreviewConverter` (Note -> single-line preview string).

- [ ] **Step 1: Append `NotePreviewConverter` to Converters.cs**

The Text block's `RichText` is Base64 `XamlPackage` (or legacy XAML, or plain text). This converter mirrors `NoteEditorView.DeserializeIntoRichTextBox` to extract plain text. Add to `src/NoteApp/Converters/Converters.cs`:

```csharp
public sealed class NotePreviewConverter : System.Windows.Data.IValueConverter
{
    private const int MaxLength = 120;

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not NoteApp.Domain.Models.Note note || note.IsEncrypted)
            return string.Empty;

        var firstText = note.Blocks
            .OrderBy(b => b.SortOrder)
            .OfType<NoteApp.Domain.Models.NoteBlock.Text>()
            .FirstOrDefault();
        if (firstText is null || string.IsNullOrEmpty(firstText.RichText))
            return string.Empty;

        var plain = ExtractPlainText(firstText.RichText);
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ").Trim();
        return plain.Length > MaxLength ? plain[..MaxLength] + "…" : plain;
    }

    private static string ExtractPlainText(string content)
    {
        var doc = new System.Windows.Documents.FlowDocument();
        // Try Base64 XamlPackage first
        try
        {
            var bytes = System.Convert.FromBase64String(content);
            using var ms = new System.IO.MemoryStream(bytes);
            var range = new System.Windows.Documents.TextRange(doc.ContentStart, doc.ContentEnd);
            range.Load(ms, System.Windows.DataFormats.XamlPackage);
            return new System.Windows.Documents.TextRange(doc.ContentStart, doc.ContentEnd).Text;
        }
        catch { /* not XamlPackage */ }

        // Try legacy plain XAML
        try
        {
            if (System.Windows.Markup.XamlReader.Parse(content) is System.Windows.Documents.FlowDocument parsed)
                return new System.Windows.Documents.TextRange(parsed.ContentStart, parsed.ContentEnd).Text;
        }
        catch { /* not XAML */ }

        // Last resort: treat as plain text
        return content;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Build (converter compiles)**

Run: `dotnet build NoteApp.slnx`
Expected: Build succeeds.

- [ ] **Step 3: Simplify NoteListViewModel — remove the view toggle, open on selection**

In `src/NoteApp/ViewModels/NoteListViewModel.cs`:

a) Delete the `IsListView` field (line 49) and the `ToggleViewMode` command (lines 69-70):

```csharp
// DELETE: [ObservableProperty] private bool _isListView;
// DELETE:
//   [RelayCommand]
//   private void ToggleViewMode() => IsListView = !IsListView;
```

b) Add an open-on-select partial method next to the other `partial void On...Changed` methods (after `OnSelectedSortChanged`, around line 76):

```csharp
    partial void OnSelectedNoteChanged(Note? value)
    {
        if (value is not null)
            EditNoteRequested?.Invoke(value);
    }
```

- [ ] **Step 4: Rewrite the list presentation in NoteListView.xaml**

In `src/NoteApp/Views/NoteListView.xaml`:

a) Add the preview converter and replace the green tag-badge brushes with violet ones. Replace the `<UserControl.Resources>` block (lines 8-19) with:

```xml
<UserControl.Resources>
    <converters:NoteTypeToIconConverter x:Key="NoteTypeToIcon" />
    <converters:BoolToVisibilityConverter x:Key="BoolToVis" />
    <converters:NotePreviewConverter x:Key="NotePreview" />

    <SolidColorBrush x:Key="TagBadgeBackground" Color="#E6E8FF" />
    <SolidColorBrush x:Key="TagBadgeForeground" Color="#3A3F8F" />
    <SolidColorBrush x:Key="TagBadgeBorder" Color="#C5CAF5" />
    <SolidColorBrush x:Key="TagBadgeSelectedBackground" Color="#6C79FF" />
    <SolidColorBrush x:Key="TagBadgeSelectedForeground" Color="#FFFFFF" />
    <SolidColorBrush x:Key="TagBadgeSelectedBorder" Color="#6C79FF" />
</UserControl.Resources>
```

b) Remove the `InverseBoolToVisibilityConverter` usage. In the filter `DockPanel` (lines 54-120), delete the entire right-side `StackPanel` containing the Sort `ComboBox` and the `IsListView` `ToggleButton` (lines 55-74) — replace it with just the Sort combo kept on the right:

```xml
<!-- Sort (right side) -->
<ComboBox DockPanel.Dock="Right" ItemsSource="{Binding SortOptions}"
          SelectedItem="{Binding SelectedSort}"
          materialDesign:HintAssist.Hint="Sort by"
          Width="160" FontSize="12" Height="32" Margin="8,0,0,0"
          VerticalAlignment="Center"
          materialDesign:ComboBoxAssist.ShowSelectedItem="True">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Label}" />
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

c) Replace **both** the Card `ScrollViewer` (lines 173-345) and the `DataGrid` (lines 347-458) with a single Comfortable `ListBox`:

```xml
<!-- Comfortable single-column note list -->
<ListBox Grid.Row="3" ItemsSource="{Binding Notes}" Margin="8,0,8,8"
         SelectedItem="{Binding SelectedNote}"
         HorizontalContentAlignment="Stretch"
         ScrollViewer.HorizontalScrollBarVisibility="Disabled"
         Background="Transparent" BorderThickness="0">
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem" BasedOn="{StaticResource {x:Type ListBoxItem}}">
            <Setter Property="Padding" Value="0" />
            <Setter Property="Margin" Value="0,2" />
            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
        </Style>
    </ListBox.ItemContainerStyle>
    <ListBox.ItemTemplate>
        <DataTemplate>
            <Border Background="{DynamicResource MaterialDesignCardBackground}"
                    CornerRadius="{StaticResource CardCornerRadius}"
                    Padding="12,10" Margin="4,2">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>

                    <!-- Title row: icon + title + type dots -->
                    <DockPanel Grid.Row="0">
                        <materialDesign:PackIcon Width="18" Height="18" DockPanel.Dock="Left"
                                                 Margin="0,0,8,0" VerticalAlignment="Center"
                                                 Foreground="{StaticResource AccentBrush}">
                            <materialDesign:PackIcon.Style>
                                <Style TargetType="materialDesign:PackIcon">
                                    <Setter Property="Kind" Value="Note" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsEncrypted}" Value="True">
                                            <Setter Property="Kind" Value="Lock" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </materialDesign:PackIcon.Style>
                        </materialDesign:PackIcon>

                        <!-- Type dots (right) -->
                        <StackPanel Orientation="Horizontal" DockPanel.Dock="Right" VerticalAlignment="Center">
                            <materialDesign:PackIcon Kind="NoteText" Width="13" Height="13" Margin="3,0,0,0"
                                                     Foreground="{DynamicResource MaterialDesignBodyLight}"
                                                     Visibility="{Binding HasText, Converter={StaticResource BoolToVis}}" />
                            <materialDesign:PackIcon Kind="FileDocument" Width="13" Height="13" Margin="3,0,0,0"
                                                     Foreground="{DynamicResource MaterialDesignBodyLight}"
                                                     Visibility="{Binding HasFiles, Converter={StaticResource BoolToVis}}" />
                            <materialDesign:PackIcon Kind="Link" Width="13" Height="13" Margin="3,0,0,0"
                                                     Foreground="{DynamicResource MaterialDesignBodyLight}"
                                                     Visibility="{Binding HasLinks, Converter={StaticResource BoolToVis}}" />
                        </StackPanel>

                        <TextBlock Text="{Binding Title.Value}" FontWeight="Medium" FontSize="14"
                                   TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
                    </DockPanel>

                    <!-- Preview snippet -->
                    <TextBlock Grid.Row="1" Margin="26,2,0,4"
                               Text="{Binding Converter={StaticResource NotePreview}}"
                               FontSize="12" TextTrimming="CharacterEllipsis" MaxHeight="18"
                               Foreground="{DynamicResource MaterialDesignBodyLight}" />

                    <!-- Tags + date -->
                    <DockPanel Grid.Row="2" Margin="26,0,0,0">
                        <TextBlock DockPanel.Dock="Right" VerticalAlignment="Center"
                                   Text="{Binding UpdatedAt, StringFormat='{}{0:MMM dd}'}"
                                   FontSize="11" Foreground="{DynamicResource MaterialDesignBodyLight}" />
                        <ItemsControl ItemsSource="{Binding Tags}">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate><WrapPanel /></ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Border Background="{StaticResource TagBadgeBackground}"
                                            BorderBrush="{StaticResource TagBadgeBorder}"
                                            BorderThickness="1" CornerRadius="9"
                                            Padding="8,2" Margin="0,0,4,0">
                                        <TextBlock Text="{Binding Name.Value}" FontSize="10"
                                                   FontWeight="SemiBold"
                                                   Foreground="{StaticResource TagBadgeForeground}" />
                                    </Border>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </DockPanel>
                </Grid>
            </Border>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

d) The "Empty state" `StackPanel` (lines 460-479) stays. Remove the now-unused `InverseBoolToVisibilityConverter` resource declaration (deleted in step 4a) — confirm no other references remain (search the file for `InverseBoolToVis`; there should be none).

- [ ] **Step 5: Build**

Run: `dotnet build NoteApp.slnx`
Expected: Build succeeds with no binding-resource errors.

- [ ] **Step 6: Run and verify the Comfortable list**

Run: `dotnet run --project src/NoteApp`
Verify:
- The middle pane shows one note per row: type/lock icon · title · type dots; a one-line preview beneath; tag chips (violet) + short date ("Jun 27") on the bottom row.
- Encrypted notes show the lock icon and no preview text.
- **Single-clicking a row opens that note in the editor pane.**
- Search, type filters, the collapsible tag-filter panel, and sort still work. The old card/grid toggle button is gone.

- [ ] **Step 7: Commit**

```bash
git add src/NoteApp/Views/NoteListView.xaml src/NoteApp/ViewModels/NoteListViewModel.cs src/NoteApp/Converters/Converters.cs
git commit -m "$(cat <<'EOF'
feat: Comfortable single-column note list with preview snippets

Replaces the card/DataGrid toggle with one density; adds NotePreview
converter (XamlPackage->plain text); single-click opens in editor pane.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Restyle editor, tag manager, password dialog

Apply MD3/violet polish to the three remaining views so they fit the panes/modal. These are visual edits — keep all `x:Name`s, event handler names, command bindings, and `Tag` bindings intact (the `NoteEditorView` code-behind depends on them).

**Files:**
- Modify: `src/NoteApp/Views/NoteEditorView.xaml`
- Modify: `src/NoteApp/Views/TagManagerView.xaml`
- Modify: `src/NoteApp/Views/PasswordDialog.xaml`

- [ ] **Step 1: Read the three views**

Read each file fully before editing so existing structure/names are preserved:
`src/NoteApp/Views/NoteEditorView.xaml`, `src/NoteApp/Views/TagManagerView.xaml`, `src/NoteApp/Views/PasswordDialog.xaml`.

- [ ] **Step 2: NoteEditorView — pane-friendly violet polish**

Apply only these, without restructuring:
- Wrap top-level content padding to `16` if not already; ensure the root has `Background="{DynamicResource MaterialDesignCardBackground}"` so it matches the editor pane.
- On any `materialDesign:Card`/`Border` containing a block, set `CornerRadius="{StaticResource CardCornerRadius}"`.
- Primary action buttons (Save / Export) → `Style="{StaticResource MaterialDesignRaisedButton}"`; secondary (Cancel) → `MaterialDesignOutlinedButton`.
- Recolor any green tag selection brushes to the violet `TagBadge*` values from Task 3 Step 4a (same hex). Replace literal greens (`#C8E6C9`, `#2E7D32`, `#1B5E20`, `#81C784`) with the violet equivalents.

- [ ] **Step 3: TagManagerView — middle-pane fit**

- Root `Background="{DynamicResource MaterialDesignPaper}"`, padding `16`.
- Tag rows/chips: `CornerRadius="{StaticResource ControlCornerRadius}"`; recolor greens to violet `TagBadge*` values.
- Ensure the list fills the pane (`HorizontalAlignment="Stretch"`).

- [ ] **Step 4: PasswordDialog — modal polish**

- Wrap content in adequate padding (`24`) and set a sensible `Width` (~360) if not set.
- Primary confirm button → `MaterialDesignRaisedButton` with accent; cancel → `MaterialDesignOutlinedButton`.
- Round the dialog surface corners via the containing `Border`/`Card` `CornerRadius="{StaticResource CardCornerRadius}"`.

- [ ] **Step 5: Build**

Run: `dotnet build NoteApp.slnx`
Expected: Build succeeds.

- [ ] **Step 6: Run and verify**

Run: `dotnet run --project src/NoteApp`
Verify: editor pane, tag manager (via rail Tags), and the encryption password dialog all render with rounded violet styling; block add/remove/move, tag toggling, file attach, search-in-note, and PDF export buttons still function (handlers intact).

- [ ] **Step 7: Commit**

```bash
git add src/NoteApp/Views/NoteEditorView.xaml src/NoteApp/Views/TagManagerView.xaml src/NoteApp/Views/PasswordDialog.xaml
git commit -m "$(cat <<'EOF'
style: MD3/violet polish for editor, tag manager, password dialog

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Dark mode verification + docs

Dark mode is already wired (`SettingsViewModel.ApplyTheme`, applied at startup in `App.xaml.cs:55` and on toggle). The rail is intentionally navy in both modes; surfaces use MDIX dynamic brushes that auto-switch. This task confirms coverage and updates docs.

**Files:**
- Modify (only if a gap is found): `src/NoteApp/Theme/ModernViolet.xaml`, affected views
- Modify: `.claude/CLAUDE.md` (architecture notes)

- [ ] **Step 1: Verify dark mode across the redesign**

Run: `dotnet run --project src/NoteApp`
Open Settings (rail) → toggle dark mode. Verify in dark mode: middle pane, note rows/preview text, tag chips, editor pane, tag manager, password dialog, and the empty-state are all legible (sufficient contrast; no white-on-white or black-on-black). The navy rail stays navy.

- [ ] **Step 2: Fix any dark-mode contrast gaps**

For any element using a **static** custom brush that is illegible in dark mode (e.g., a hardcoded light text color), switch it to the appropriate `{DynamicResource MaterialDesignBody}` / `MaterialDesignBodyLight` / `MaterialDesignPaper` / `MaterialDesignCardBackground`. The violet tag-chip `#E6E8FF/#3A3F8F` is legible on dark surfaces (light chip, dark text) — leave unless verification shows otherwise. Make the minimal change, rebuild (`dotnet build NoteApp.slnx`), and re-verify.

- [ ] **Step 3: Update CLAUDE.md architecture notes**

In `.claude/CLAUDE.md`, under **Key Patterns**, replace the two stale bullets describing the hamburger drawer and the green tag-filter fill with notes describing the three-pane shell (`MainWindow.xaml`: rail · `MiddlePaneContent` · `CurrentEditor`, Settings in `RootDialog`), the Comfortable single-column list with `NotePreviewConverter`, and the Modern Violet MD3 theme (`Theme/ModernViolet.xaml`). Keep it to a few lines consistent with the surrounding style.

- [ ] **Step 4: Final full verification pass**

Run: `dotnet run --project src/NoteApp`
Exercise end-to-end: create a note (text + file + link blocks) → it appears in the list with a preview → edit it → encrypt it (password dialog) → reopen (decrypt) → filter by tag → switch to Tags pane and back → toggle dark mode → open Settings and run a backup if the DB is available. Confirm no regressions.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
docs: update CLAUDE.md for three-pane redesign; dark-mode fixes

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review

**Spec coverage:**
- Three-pane shell → Task 2. ✓
- Modern Violet + MD3 → Task 1. ✓
- Dark mode → Task 1 (palette) + Task 5 (verify/fix). ✓
- Comfortable list density + preview snippet → Task 3. ✓
- Mixed rail nav (Tags swaps middle pane; Settings modal) → Task 2. ✓
- Restyle editor/tag manager/password dialog → Task 4. ✓
- Remove card/grid toggle → Task 3. ✓
- Non-goals (no services/domain/EF changes) → respected; only `MainViewModel`/`NoteListViewModel` orchestration + XAML + converters touched. ✓
- Risk: "editor empty-state via XAML on null" → implemented in Task 2 Step 3 (`NullToVisibilityConverter`). ✓
- Risk: window min-width → raised to 980 in Task 2 Step 3. ✓

**Placeholder scan:** No TBD/TODO; every code step contains full code. Task 4 restyle steps give exact brushes/keys/values rather than full file rewrites because they preserve existing named elements the code-behind binds to — the specific edits are enumerated.

**Type consistency:** `MiddlePaneContent`/`CurrentEditor`/`IsSettingsOpen` and commands `NavigateToNotesCommand`/`NavigateToTagsCommand`/`OpenSettingsCommand`/`CreateNoteCommand` are defined in Task 2 Step 2 and consumed by the same task's MainWindow.xaml (Step 3). `NullToVisibilityConverter` (Task 2) and `NotePreviewConverter` (Task 3) are defined before use. `OnSelectedNoteChanged` matches the existing `[ObservableProperty] private Note? _selectedNote`. Tag brush hexes are identical across Task 3 and Task 4.
