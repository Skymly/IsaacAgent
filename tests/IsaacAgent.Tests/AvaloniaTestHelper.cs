using Avalonia.Headless;
using Avalonia.Threading;
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
///   Ensures <see cref="HeadlessUnitTestSession"/> is started once per
///   collection. Do not call AppBuilder.Setup* manually here.
/// </summary>
public sealed class AvaloniaFixture
{
    public AvaloniaFixture()
    {
        _ = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaFixture).Assembly);
    }
}

/// <summary>
///   Helpers for Avalonia headless tests.
/// </summary>
internal static class AvaloniaTestHelper
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTestHelper).Assembly);

    /// <summary>
    ///   Pumps the Avalonia dispatcher queue so pending Post callbacks
    ///   execute before assertions.
    /// </summary>
    public static void FlushDispatcher()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.RunJobs();
            return;
        }

        Session.Dispatch(() => Dispatcher.UIThread.RunJobs(), CancellationToken.None);
    }

    /// <summary>
    ///   Runs an action on the Avalonia UI thread with proper dispatcher
    ///   pumping (safe from any thread).
    /// </summary>
    public static void Dispatch(Action action)
        => Session.Dispatch(action, CancellationToken.None);

    /// <summary>
    ///   Runs an async action on the Avalonia UI thread with proper
    ///   dispatcher pumping (safe from any thread).
    /// </summary>
    public static Task DispatchAsync(Func<Task> action)
        => Session.Dispatch(action, CancellationToken.None);
}
