using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using IsaacAgent.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IsaacAgent.App.Views;

public sealed partial class MainWindow : Window
{
    /// <summary>Max bytes for a dropped file injected into chat input.</summary>
    internal const long MaxDropFileBytes = 256 * 1024;

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

        RestoreWindowState();

        // Populate Lua snippet dropdown from the injected service.
        var snippetService = App.Services.GetRequiredService<LuaSnippetService>();
        var snippetCombo = this.FindControl<ComboBox>("SnippetComboBox");
        if (snippetCombo is not null)
        {
            snippetCombo.ItemsSource = snippetService.FilteredSnippets;
        }

        // Enable drag-drop: folders open as project, files are read into chat context.
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
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

        SaveWindowState();

        base.OnClosed(e);
    }

    /// <summary>
    ///   Restores window size, position, and maximized state from
    ///   persisted AppConfiguration. Falls back to XAML defaults if
    ///   no saved state exists or values are invalid.
    /// </summary>
    private void RestoreWindowState()
    {
        var config = App.Services.GetRequiredService<AppConfiguration>();
        if (config.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        if (config.WindowWidth > 200 && config.WindowHeight > 200)
        {
            Width = config.WindowWidth;
            Height = config.WindowHeight;
        }

        if (config.WindowX is { } x && config.WindowY is { } y
            && x >= -10000 && y >= -10000)
        {
            Position = new Avalonia.PixelPoint((int)x, (int)y);
        }
    }

    /// <summary>
    ///   Saves the current window size, position, and maximized state
    ///   to AppConfiguration so it can be restored on next launch.
    /// </summary>
    private void SaveWindowState()
    {
        var config = App.Services.GetRequiredService<AppConfiguration>();
        var isMaximized = WindowState == WindowState.Maximized;

        config.WindowMaximized = isMaximized;

        if (!isMaximized)
        {
            config.WindowWidth = Width;
            config.WindowHeight = Height;
            var pos = Position;
            config.WindowX = pos.X;
            config.WindowY = pos.Y;
        }

        config.Save();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // ── Drag-and-drop ──────────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var data = e.Data;
        if (data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.Contains(DataFormats.Files)) return;

        var items = e.Data.GetFiles()?.ToList();
        if (items is null || items.Count == 0) return;

        // First folder dropped opens as project
        var folder = items.FirstOrDefault(f => f.Path.IsAbsoluteUri && Directory.Exists(f.Path.LocalPath));
        if (folder is not null)
        {
            var path = folder.Path.LocalPath;
            if (_mainVm is not null)
                await _mainVm.Project.LoadProjectAsync(path);
            return;
        }

        // Otherwise, read dropped files and inject as chat context
        var file = items.FirstOrDefault(f => f.Path.IsAbsoluteUri && File.Exists(f.Path.LocalPath));
        if (file is not null && _mainVm is not null)
        {
            var filePath = file.Path.LocalPath;
            try
            {
                var length = new FileInfo(filePath).Length;
                if (length > MaxDropFileBytes)
                {
                    _mainVm.Toasts.ShowError(
                        $"Dropped file is too large ({length / 1024} KB). Max is {MaxDropFileBytes / 1024} KB.");
                    return;
                }

                var content = await File.ReadAllTextAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                var tab = _mainVm.Chat.ActiveTab;
                if (tab is not null)
                {
                    tab.InputText = $"Here is the content of `{fileName}`:\n\n```lua\n{content}\n```";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drop file read failed: {ex.Message}");
                _mainVm.Toasts.ShowError($"Could not read dropped file: {ex.Message}");
            }
        }
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
        FileTreeItem? item = null;
        if (sender is TreeView tv && tv.SelectedItem is FileTreeItem treeItem)
            item = treeItem;
        else if (sender is ListBox lb && lb.SelectedItem is FileTreeItem listItem)
            item = listItem;

        if (item is { IsDirectory: false })
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

    private void OnSnippetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.SelectedItem is not LuaSnippet snippet) return;

        var vm = DataContext as MainViewModel;
        if (vm?.Chat.ActiveTab is { } tab)
        {
            tab.InsertSnippetCommand.Execute(snippet.Code);
        }

        // Reset selection so the same snippet can be selected again
        cb.SelectedIndex = -1;
    }

    private void OnSnippetSearchChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var snippetService = App.Services.GetRequiredService<LuaSnippetService>();
        snippetService.SearchText = tb.Text ?? "";
    }

    private void OnSnippetManage(object? sender, RoutedEventArgs e)
    {
        var window = new SnippetManagerWindow
        {
            DataContext = App.Services.GetRequiredService<LuaSnippetService>()
        };
        window.Show(this);
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm is not null)
        {
            // Ctrl+Shift+P: Command palette
            if (e.Key == Avalonia.Input.Key.P && e.KeyModifiers == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift))
            {
                OpenCommandPalette();
                e.Handled = true;
                return;
            }
            // Ctrl+Shift+T: Template gallery
            if (e.Key == Avalonia.Input.Key.T && e.KeyModifiers == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift))
            {
                OpenTemplateGallery();
                e.Handled = true;
                return;
            }
            // Ctrl+Shift+D: Diff viewer
            if (e.Key == Avalonia.Input.Key.D && e.KeyModifiers == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift))
            {
                OpenDiffViewer();
                e.Handled = true;
                return;
            }
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
            // Ctrl+Tab: Switch to next chat tab
            if (e.Key == Avalonia.Input.Key.Tab && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                vm.Chat.SwitchToNextTabCommand.Execute(null);
                e.Handled = true;
                return;
            }
            // Ctrl+W: Close active tab (if more than one tab)
            if (e.Key == Avalonia.Input.Key.W && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                if (vm.Chat.CanCloseTabs && vm.Chat.ActiveTab is not null)
                {
                    vm.Chat.CloseTabCommand.Execute(vm.Chat.ActiveTab);
                }
                e.Handled = true;
                return;
            }
            // F1: About
            if (e.Key == Avalonia.Input.Key.F1 && e.KeyModifiers == Avalonia.Input.KeyModifiers.None)
            {
                OnAbout(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            // Ctrl+F: Toggle search
            if (e.Key == Avalonia.Input.Key.F && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            {
                vm.ToggleSearchCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private void OpenCommandPalette()
    {
        var dialog = new CommandPaletteWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog(this);
    }

    private void OnCommandPalette(object? sender, RoutedEventArgs e) => OpenCommandPalette();

    private void OpenTemplateGallery()
    {
        var dialog = new TemplateGalleryWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog(this);
    }

    private void OnTemplateGallery(object? sender, RoutedEventArgs e) => OpenTemplateGallery();

    private void OpenDiffViewer()
    {
        var vm = DataContext as MainViewModel;
        var dialog = new DiffViewerWindow();
        var diffVm = App.Services.GetRequiredService<DiffViewerViewModel>();
        diffVm.SetProjectDir(vm?.Project?.ProjectPath);
        dialog.DataContext = diffVm;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.ShowDialog(this);
    }

    private void OnDiffViewer(object? sender, RoutedEventArgs e) => OpenDiffViewer();

    private void OnLogMonitorStart(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        vm?.LogMonitor.Start();
    }

    private void OnLogMonitorStop(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        vm?.LogMonitor.Stop();
    }

    private void OnLogMonitorClear(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        vm?.LogMonitor.Clear();
    }

    internal void OpenSettings() => OnSettings(this, new RoutedEventArgs());

    internal void ShowAbout() => OnAbout(this, new RoutedEventArgs());

    internal void FocusFileList()
    {
        // Focus the chat input as the primary interaction point
        ChatInputBox.Focus();
    }

    internal void FocusChatInput()
    {
        ChatInputBox.Focus();
        // Move caret to end so the user can continue typing after a slash command
        if (ChatInputBox.Text is { } text && text.Length > 0)
            ChatInputBox.CaretIndex = text.Length;
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
        var dialog = new AboutWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog(this);
    }
}
