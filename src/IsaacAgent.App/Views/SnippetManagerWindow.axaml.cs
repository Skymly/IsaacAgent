using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using IsaacAgent.App.Services;

namespace IsaacAgent.App.Views;

public sealed partial class SnippetManagerWindow : Window
{
    private LuaSnippetService? _service;
    private LuaSnippet? _selectedSnippet;

    public SnippetManagerWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _service = DataContext as LuaSnippetService;
        if (_service is not null)
        {
            SnippetList.ItemsSource = _service.GroupedSnippets;
        }
    }

    private void OnAddSnippet(object? sender, RoutedEventArgs e)
    {
        if (_service is null) return;

        var name = NameBox.Text?.Trim();
        var category = CategoryBox.Text?.Trim();
        var desc = DescBox.Text?.Trim();
        var code = CodeBox.Text?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
            return;

        var snippet = new LuaSnippet
        {
            Name = name,
            Category = string.IsNullOrEmpty(category) ? "Custom" : category,
            Description = desc ?? "",
            Code = code
        };

        if (_service.AddCustom(snippet))
        {
            // Clear form
            NameBox.Text = "";
            CategoryBox.Text = "";
            DescBox.Text = "";
            CodeBox.Text = "";
            // Refresh grouped list
            SnippetList.ItemsSource = _service.GroupedSnippets;
        }
    }

    private void OnDeleteSnippet(object? sender, RoutedEventArgs e)
    {
        if (_service is null || _selectedSnippet is null) return;
        if (!_selectedSnippet.IsCustom) return;

        _service.RemoveCustom(_selectedSnippet.Name);
        _selectedSnippet = null;
        DeleteBtn.IsEnabled = false;
        SnippetList.ItemsSource = _service.GroupedSnippets;
    }

    private void OnSnippetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is LuaSnippet snippet)
        {
            _selectedSnippet = snippet;
            DeleteBtn.IsEnabled = snippet.IsCustom;
        }
    }
}
