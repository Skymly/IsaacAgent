using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;

namespace IsaacAgent.App;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
