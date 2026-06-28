using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace IsaacAgent.App.Views;

public sealed partial class AboutWindow : Window
{
    public string VersionText { get; } = $"Version {GetAppVersion()}";

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static string GetAppVersion()
    {
        var attr = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attr?.InformationalVersion ?? "0.0.0";
    }
}
