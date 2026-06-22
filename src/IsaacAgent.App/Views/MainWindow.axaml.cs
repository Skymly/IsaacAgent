using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IsaacAgent.App.ViewModels;
using IsaacAgent.App.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;

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

        vm.Chat.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.UIThread.Post(() => ChatScrollViewer.ScrollToEnd(), DispatcherPriority.Background);
        }
    }

    private void OnFileDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is FileTreeItem item)
        {
            var vm = DataContext as MainViewModel;
            vm?.Project.OpenFileCommand.Execute(item);
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnSettings(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog(this);
    }

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
