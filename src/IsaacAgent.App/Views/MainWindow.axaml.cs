using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace IsaacAgent.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = vm;

        vm.Project.PickFolderAsync = async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select mod project folder",
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0] : null;
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnAbout(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "About IsaacAgent",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "IsaacAgent", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = "AI Coding Agent for Binding of Isaac: Repentance Modding", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new TextBlock { Text = "Version 0.1.0", Opacity = 0.6 },
                    new TextBlock { Text = "Built with Avalonia + .NET 8", Opacity = 0.6 }
                }
            }
        };
        dialog.ShowDialog(this);
    }
}
