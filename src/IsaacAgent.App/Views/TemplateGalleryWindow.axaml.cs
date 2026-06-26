using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Views;

public sealed partial class TemplateGalleryWindow : Window
{
    private readonly TemplateGalleryViewModel _vm;

    public TemplateGalleryWindow()
    {
        InitializeComponent();
        _vm = new TemplateGalleryViewModel();
        DataContext = _vm;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    public async Task<(string[]? Files, string? Error)> ScaffoldWithFolderPickerAsync(Window owner)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder for new mod project",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return (null, null);

        var targetDir = folders[0].Path.LocalPath;
        return await _vm.ScaffoldIntoAsync(targetDir);
    }
}
