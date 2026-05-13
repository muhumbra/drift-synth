using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Drift.Ui.Controls;
using Drift.Ui.ViewModels;

namespace Drift.Ui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnGlobalKeyDown, handledEventsToo: true);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private static void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            MidiCcRegistry.Map?.Disarm();
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        Vm?.TogglePlay();
    }

    private void OnAsioSettingsClick(object? sender, RoutedEventArgs e)
    {
        Vm?.OpenAsioPanel();
    }

    private void OnPanicClick(object? sender, RoutedEventArgs e)
    {
        Vm?.Panic();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Vm?.SaveCurrentPatch();
    }

    private void OnRefreshPatchesClick(object? sender, RoutedEventArgs e)
    {
        Vm?.RefreshPatches();
    }

    private void OnArpClearClick(object? sender, RoutedEventArgs e)
    {
        Vm?.ClearArp();
    }

    private void OnRandomPatchClick(object? sender, RoutedEventArgs e)
    {
        Vm?.RandomizePatch();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        var sp = StorageProvider;
        var startFolder = await sp.TryGetFolderFromPathAsync(Vm.SuggestedSaveAsFolder());
        var picker = new FilePickerSaveOptions
        {
            Title = "Save Patch As",
            SuggestedFileName = $"{SafeName(Vm.Patch.Name)}.dpatch.json",
            DefaultExtension = "dpatch.json",
            ShowOverwritePrompt = true,
            SuggestedStartLocation = startFolder,
            FileTypeChoices =
            [
                new FilePickerFileType("Drift Patch")
                {
                    Patterns = ["*.dpatch.json", "*.json"]
                }
            ]
        };

        var file = await sp.SaveFilePickerAsync(picker);
        if (file is null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        Vm.SaveAs(path);
    }

    private static string SafeName(string s)
    {
        var chars = Path.GetInvalidFileNameChars();
        foreach (var c in chars)
        {
            s = s.Replace(c, '-');
        }

        return s.Replace(' ', '-').Replace(':', '-').ToLowerInvariant();
    }
}