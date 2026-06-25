using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IsaacAgent.App.ViewModels;
using IsaacAgent.App.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.ComponentModel;

namespace IsaacAgent.App.Views;

public sealed partial class MainWindow : Window
{
    private ChatTabViewModel? _scrollTab;
    private MainViewModel? _mainVm;

    public MainWindow()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MainViewModel>();
        _mainVm = vm;
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

        // Subscribe to active tab message changes for auto-scroll.
        // Unsubscribe from the previous tab to avoid handler accumulation.
        vm.Chat.PropertyChanged += OnChatPropertyChanged;
        _scrollTab = vm.Chat.ActiveTab;
        if (_scrollTab is not null)
            _scrollTab.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatViewModel.ActiveTab)) return;
        if (_scrollTab is not null)
            _scrollTab.Messages.CollectionChanged -= OnMessagesChanged;
        _scrollTab = _mainVm?.Chat.ActiveTab;
        if (_scrollTab is not null)
            _scrollTab.Messages.CollectionChanged += OnMessagesChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe all event handlers to prevent leaks.
        if (_mainVm is not null)
            _mainVm.Chat.PropertyChanged -= OnChatPropertyChanged;
        if (_scrollTab is not null)
            _scrollTab.Messages.CollectionChanged -= OnMessagesChanged;
        _scrollTab = null;
        _mainVm = null;
        base.OnClosed(e);
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

    private void OnInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm is null || vm.Chat.ActiveTab is null) return;

        // Ctrl+Enter to send
        if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            var tab = vm.Chat.ActiveTab;
            if (!tab.IsGenerating && !string.IsNullOrWhiteSpace(tab.InputText))
            {
                tab.SendCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm is not null)
        {
            // Ctrl+K: Clear chat
            if (e.Key == Avalonia.Input.Key.K && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                vm.ClearChatCommand.Execute(null);
                e.Handled = true;
                return;
            }
            // Ctrl+N: New project
            if (e.Key == Avalonia.Input.Key.N && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                vm.NewProjectCommand.Execute(null);
                e.Handled = true;
                return;
            }
            // Ctrl+O: Open project
            if (e.Key == Avalonia.Input.Key.O && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                vm.OpenProjectCommand.Execute(null);
                e.Handled = true;
                return;
            }
            // Ctrl+,: Settings
            if (e.Key == Avalonia.Input.Key.OemComma && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                OnSettings(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

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
