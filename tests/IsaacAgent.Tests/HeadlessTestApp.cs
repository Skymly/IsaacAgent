using Avalonia;
using Avalonia.Headless;

namespace IsaacAgent.Tests;

/// <summary>
///   Headless Avalonia application used by <see cref="HeadlessUnitTestSession"/>.
/// </summary>
public sealed class HeadlessTestApp : Avalonia.Application
{
    public override void Initialize() { }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
