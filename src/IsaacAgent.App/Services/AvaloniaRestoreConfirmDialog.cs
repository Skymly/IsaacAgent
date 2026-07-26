using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace IsaacAgent.App.Services;

/// <summary>
/// Avalonia implementation of the Checkpoint Restore confirm dialog.
/// </summary>
public sealed class AvaloniaRestoreConfirmDialog : IRestoreConfirmDialog
{
    public async Task<bool> ConfirmRestoreAsync(
        RestoreConfirmCopy copy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(copy);

        var owner = GetMainWindow();
        var window = new Window
        {
            Title = copy.Title,
            Width = 480,
            MinHeight = 280,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var result = false;
        var facts = new StackPanel { Spacing = 8 };
        foreach (var fact in new[]
                 {
                     copy.TruncateFact,
                     copy.BeforeImageFact,
                     copy.CancelInFlightFact,
                     copy.RefillInputFact,
                     copy.UntrackedFact
                 })
        {
            facts.Children.Add(new TextBlock
            {
                Text = "• " + fact,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var confirm = new Button
        {
            Content = ResolveString("ChatRestoreConfirm", "Restore"),
            Classes = { "primary" },
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 88
        };
        var cancel = new Button
        {
            Content = ResolveString("SettingsCancel", "Cancel"),
            Classes = { "ghost" },
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 88
        };

        confirm.Click += (_, _) =>
        {
            result = true;
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { cancel, confirm }
        };

        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = copy.Title,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold
                },
                facts,
                buttons
            }
        };

        if (owner is not null)
            await window.ShowDialog(owner);
        else
            window.Show();

        return result;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private static string ResolveString(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is string s
            && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return fallback;
    }
}
