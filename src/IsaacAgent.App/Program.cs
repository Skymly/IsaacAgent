using Avalonia;
using IsaacAgent.App.Services;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;

namespace IsaacAgent.App;

internal sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--verify-onnx", StringComparison.OrdinalIgnoreCase)))
            return OnnxPublishVerifier.Run();

        IconProvider.Current.Register<MaterialDesignIconProvider>();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
