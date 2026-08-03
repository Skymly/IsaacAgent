using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace IsaacAgent.UiTests;

/// <summary>
/// Launches the Release App build and tears down the process (including orphans on failure).
/// </summary>
internal sealed class AppSession : IDisposable
{
    private readonly Application _application;
    private readonly UIA3Automation _automation;
    private bool _disposed;

    private AppSession(Application application, UIA3Automation automation)
    {
        _application = application;
        _automation = automation;
    }

    public int ProcessId => _application.ProcessId;

    public static AppSession Launch(params string[] args)
    {
        var exe = AppExecutable.ResolveReleasePath();
        var startInfo = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe) ?? ".",
            UseShellExecute = false,
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        var application = Application.Launch(startInfo);
        var automation = new UIA3Automation();
        return new AppSession(application, automation);
    }

    public AutomationElement WaitForAutomationId(string automationId, TimeSpan timeout)
    {
        ThrowIfDisposed();
        var result = Retry.WhileNull(
            () => FindByAutomationId(automationId),
            timeout,
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: true,
            ignoreException: true,
            timeoutMessage: $"AutomationId '{automationId}' not found within {timeout}.");
        return result.Result
            ?? throw new TimeoutException($"AutomationId '{automationId}' not found within {timeout}.");
    }

    public AutomationElement? TryFindAutomationId(string automationId, TimeSpan timeout)
    {
        ThrowIfDisposed();
        return Retry.WhileNull(
            () => FindByAutomationId(automationId),
            timeout,
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: false,
            ignoreException: true).Result;
    }

    public bool CloseMainWindowCleanly(TimeSpan exitTimeout)
    {
        ThrowIfDisposed();
        _application.CloseTimeout = exitTimeout;
        // Prefer graceful CloseMainWindow; only kill if the process ignores the close.
        var closedGracefully = _application.Close(killIfCloseFails: false);
        if (!_application.HasExited)
            KillProcessTree();

        if (!_application.HasExited)
            throw new InvalidOperationException(
                $"IsaacAgent process {ProcessId} is still alive after close/teardown.");

        return closedGracefully;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (!_application.HasExited)
                KillProcessTree();
        }
        finally
        {
            _automation.Dispose();
            _application.Dispose();
        }
    }

    private AutomationElement? FindByAutomationId(string automationId)
    {
        if (_application.HasExited)
            return null;

        Window? main;
        try
        {
            main = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(1));
        }
        catch
        {
            return null;
        }

        if (main is null)
            return null;

        if (string.Equals(main.AutomationId, automationId, StringComparison.Ordinal))
            return main;

        var cf = new ConditionFactory(new UIA3PropertyLibrary());
        return main.FindFirstDescendant(cf.ByAutomationId(automationId));
    }

    private void KillProcessTree()
    {
        try
        {
            if (!_application.HasExited)
                _application.Kill();
        }
        catch
        {
            // Best-effort orphan cleanup.
        }

        try
        {
            using var process = Process.GetProcessById(ProcessId);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Already exited.
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
