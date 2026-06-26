using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace IsaacAgent.App.Views;

public sealed partial class DiffViewerWindow : Window
{
    private readonly DiffViewerViewModel _vm;

    public DiffViewerWindow()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<DiffViewerViewModel>();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.RefreshCommand.ExecuteAsync(null);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
