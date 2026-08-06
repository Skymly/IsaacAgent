using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
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

    /// <summary>
    /// Brings the App main window to the foreground so menu Click works under nested hosts (Nuke).
    /// </summary>
    public void BringMainWindowToForeground()
    {
        ThrowIfDisposed();
        try
        {
            var main = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
            main?.SetForeground();
        }
        catch
        {
            // Best-effort.
        }

        try
        {
            using var process = Process.GetProcessById(ProcessId);
            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SwRestore);
                SetForegroundWindow(handle);
            }
        }
        catch
        {
            // Best-effort.
        }
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

    /// <summary>
    /// Closes a top-level window by AutomationId (e.g. Settings) without Apply.
    /// Prefers Cancel button Click; falls back to Window Close.
    /// </summary>
    public void CloseWindowByAutomationId(string automationId, TimeSpan timeout)
    {
        ThrowIfDisposed();
        var element = WaitForAutomationId(automationId, timeout);
        DismissWindowWithoutApply(element);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (FindByAutomationId(automationId) is null)
                return;
            Thread.Sleep(250);
        }

        if (FindByAutomationId(automationId) is not null)
        {
            throw new TimeoutException(
                $"AutomationId '{automationId}' still present after Close within {timeout}.");
        }
    }

    private static void DismissWindowWithoutApply(AutomationElement windowElement)
    {
        var cf = new ConditionFactory(new UIA3PropertyLibrary());
        var buttons = windowElement.FindAllDescendants(cf.ByControlType(ControlType.Button));
        foreach (var button in buttons)
        {
            var name = button.Name ?? string.Empty;
            if (IsCancelButtonName(name))
            {
                button.Click();
                return;
            }
        }

        try
        {
            if (windowElement.Patterns.Window.IsSupported)
            {
                windowElement.Patterns.Window.Pattern.Close();
                return;
            }
        }
        catch
        {
            // Fall through.
        }

        windowElement.AsWindow().Close();
    }

    private static bool IsCancelButtonName(string name) =>
        name.Equals("Cancel", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("取消", StringComparison.Ordinal) ||
        name.Equals("キャンセル", StringComparison.Ordinal) ||
        name.Equals("취소", StringComparison.Ordinal);

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

        var cf = new ConditionFactory(new UIA3PropertyLibrary());

        Window[] windows;
        try
        {
            windows = _application.GetAllTopLevelWindows(_automation);
        }
        catch
        {
            windows = [];
        }

        foreach (var window in windows)
        {
            try
            {
                if (string.Equals(window.AutomationId, automationId, StringComparison.Ordinal))
                    return window;
            }
            catch
            {
                // Avalonia popup menus may lack AutomationId; still search descendants.
            }

            try
            {
                var found = window.FindFirstDescendant(cf.ByAutomationId(automationId));
                if (found is not null)
                    return found;
            }
            catch
            {
                // Ignore flaky UIA queries on transient popups.
            }
        }

        // Avalonia submenus can appear under the desktop tree, not only app top-levels.
        try
        {
            return _automation.GetDesktop().FindFirstDescendant(cf.ByAutomationId(automationId));
        }
        catch
        {
            return null;
        }
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

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
