using System.Diagnostics;
using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// Failed / abandoned runs must not leave IsaacAgent processes behind (issue #80).
/// </summary>
[Collection("Ui")]
public sealed class OrphanProcessCleanupTests
{
    [Fact]
    public void Dispose_WithoutCleanClose_KillsLaunchedAppProcess()
    {
        int pid;
        using (var session = AppSession.Launch())
        {
            session.WaitForAutomationId(UiContract.MainWindow, TimeSpan.FromSeconds(60));
            pid = session.ProcessId;
            Assert.False(HasExited(pid));
        }

        Assert.True(
            WaitForExit(pid, TimeSpan.FromSeconds(15)),
            $"Expected IsaacAgent process {pid} to be killed by AppSession.Dispose.");
    }

    private static bool HasExited(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool WaitForExit(int pid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (HasExited(pid))
                return true;
            Thread.Sleep(200);
        }

        return HasExited(pid);
    }
}
