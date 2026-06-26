using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Views;

public sealed partial class CommandPaletteWindow : Window
{
    private readonly CommandPaletteViewModel _vm;

    public CommandPaletteWindow()
    {
        InitializeComponent();
        _vm = new CommandPaletteViewModel();
        _vm.SetCloseAction(Close);
        DataContext = _vm;
        this.AttachedToVisualTree += (_, _) => SearchBox.Focus();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _vm.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void OnCommandDoubleTapped(object? sender, RoutedEventArgs e)
    {
        _vm.ExecuteSelectedCommand.Execute(null);
    }

    private void MoveSelection(int delta)
    {
        var list = _vm.FilteredCommands;
        if (list.Count == 0) return;
        var idx = _vm.SelectedCommand is not null ? list.IndexOf(_vm.SelectedCommand) : -1;
        idx = Math.Clamp(idx + delta, 0, list.Count - 1);
        _vm.SelectedCommand = list[idx];
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        // Close when the window loses focus (e.g. user clicks elsewhere)
        Close();
        base.OnLostFocus(e);
    }
}
