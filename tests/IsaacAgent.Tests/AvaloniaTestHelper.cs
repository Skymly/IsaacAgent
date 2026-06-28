using Avalonia;
using Avalonia.Headless;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   xUnit collection that serializes all Avalonia-dependent tests.
///   Avalonia's static constructors (StyledElement, etc.) are not
///   thread-safe under parallel test execution.
/// </summary>
[CollectionDefinition("Avalonia")]
public sealed class AvaloniaTestCollection : ICollectionFixture<AvaloniaFixture> { }

/// <summary>
///   Fixture instantiated once per collection — initializes the
///   headless Avalonia application before any test in the collection runs.
/// </summary>
public sealed class AvaloniaFixture
{
    public AvaloniaFixture()
    {
        try
        {
            AppBuilder.Configure<HeadlessApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
        }
        catch
        {
            // Already initialized — safe to ignore.
        }
    }

    private sealed class HeadlessApp : Avalonia.Application
    {
        public override void Initialize() { }
    }
}

/// <summary>
///   Ensures the Avalonia headless application is initialized exactly once
///   for test classes that use a static constructor rather than the
///   collection fixture pattern.
/// </summary>
internal static class AvaloniaTestHelper
{
    private static int _initialized;
    private static readonly object _lock = new();

    public static void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initialized != 0) return;
            try
            {
                AppBuilder.Configure<HeadlessApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }
            catch
            {
                // Already initialized by another path — that's fine.
            }
            _initialized = 1;
        }
    }

    private sealed class HeadlessApp : Avalonia.Application
    {
        public override void Initialize() { }
    }
}
