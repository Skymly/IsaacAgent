using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IsaacAgent.App.ViewModels;

/// <summary>
/// A command item for the command palette.
/// </summary>
public sealed class CommandItem
{
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string? Shortcut { get; init; }
    public Action? Action { get; init; }

    public string DisplayShortcut => string.IsNullOrEmpty(Shortcut) ? "" : Shortcut;
}

/// <summary>
/// View model for the Ctrl+Shift+P command palette.
/// Provides fuzzy-searchable access to all application actions.
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly List<CommandItem> _allCommands = [];
    private Action? _closeAction;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private CommandItem? _selectedCommand;

    public ObservableCollection<CommandItem> FilteredCommands { get; } = [];

    public CommandPaletteViewModel()
    {
        RegisterCommands();
        UpdateFilteredCommands();
    }

    public void SetCloseAction(Action closeAction) => _closeAction = closeAction;

    private void RegisterCommands()
    {
        _allCommands.AddRange([
            new CommandItem { Title = "New Project", Category = "File", Shortcut = "Ctrl+N", Action = () => InvokeMain(vm => vm.NewProjectCommand.Execute(null)) },
            new CommandItem { Title = "Open Project", Category = "File", Shortcut = "Ctrl+O", Action = () => InvokeMain(vm => vm.OpenProjectCommand.Execute(null)) },
            new CommandItem { Title = "Settings", Category = "File", Shortcut = "Ctrl+,", Action = () => InvokeMain(_ => App.Services?.GetService<MainWindow>()?.OpenSettings()) },
            new CommandItem { Title = "Exit", Category = "File", Action = () => InvokeMain(_ => App.Services?.GetService<MainWindow>()?.Close()) },
            new CommandItem { Title = "Clear Chat", Category = "Chat", Shortcut = "Ctrl+K", Action = () => InvokeMain(vm => vm.ClearChatCommand.Execute(null)) },
            new CommandItem { Title = "New Chat Tab", Category = "Chat", Action = () => InvokeMain(vm => vm.Chat.AddTabCommand.Execute(null)) },
            new CommandItem { Title = "Close Chat Tab", Category = "Chat", Action = () => InvokeMain(vm => vm.Chat.CloseTabCommand.Execute(vm.Chat.ActiveTab)) },
            new CommandItem { Title = "Send Message", Category = "Chat", Shortcut = "Ctrl+Enter", Action = () => InvokeMain(vm => { if (vm.Chat.ActiveTab is { } tab && !tab.IsGenerating && !string.IsNullOrWhiteSpace(tab.InputText)) tab.SendCommand.Execute(null); }) },
            new CommandItem { Title = "Cancel Generation", Category = "Chat", Action = () => InvokeMain(vm => vm.Chat.ActiveTab?.CancelCommand.Execute(null)) },
            new CommandItem { Title = "Open File", Category = "Project", Action = () => InvokeMain(_ => App.Services?.GetService<MainWindow>()?.FocusFileList()) },
            new CommandItem { Title = "About IsaacAgent", Category = "Help", Action = () => InvokeMain(_ => App.Services?.GetService<MainWindow>()?.ShowAbout()) },
            // Skills
            new CommandItem { Title = "Create Collectible", Category = "Skill", Shortcut = "/create-item", Action = () => InvokeSkill("/create-item ") },
            new CommandItem { Title = "Create Familiar", Category = "Skill", Shortcut = "/create-familiar", Action = () => InvokeSkill("/create-familiar ") },
            new CommandItem { Title = "Debug from Log", Category = "Skill", Shortcut = "/debug", Action = () => InvokeSkill("/debug ") },
            new CommandItem { Title = "Validate Project", Category = "Skill", Shortcut = "/validate", Action = () => InvokeSkill("/validate") },
            new CommandItem { Title = "Add Callback", Category = "Skill", Shortcut = "/add-callback", Action = () => InvokeSkill("/add-callback ") },
            new CommandItem { Title = "Add Save Data", Category = "Skill", Shortcut = "/add-save-data", Action = () => InvokeSkill("/add-save-data ") },
            new CommandItem { Title = "Add Trinket", Category = "Skill", Shortcut = "/add-trinket", Action = () => InvokeSkill("/add-trinket ") },
            new CommandItem { Title = "Add Card / Rune", Category = "Skill", Shortcut = "/add-card", Action = () => InvokeSkill("/add-card ") },
            new CommandItem { Title = "Add Pill", Category = "Skill", Shortcut = "/add-pill", Action = () => InvokeSkill("/add-pill ") },
            new CommandItem { Title = "Add Boss", Category = "Skill", Shortcut = "/add-boss", Action = () => InvokeSkill("/add-boss ") },
        ]);
    }

    private static void InvokeSkill(string slashCommand)
    {
        var vm = App.Services?.GetService<MainViewModel>();
        if (vm?.Chat.ActiveTab is { } tab && !tab.IsGenerating)
        {
            tab.InputText = slashCommand;
            // Focus the chat input so the user can type their request after the command
            App.Services?.GetService<MainWindow>()?.FocusChatInput();
        }
    }

    private static void InvokeMain(Action<MainViewModel> action)
    {
        var vm = App.Services?.GetService<MainViewModel>();
        if (vm is not null) action(vm);
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredCommands();
    }

    private void UpdateFilteredCommands()
    {
        FilteredCommands.Clear();
        var query = SearchText.Trim();

        var results = string.IsNullOrEmpty(query)
            ? _allCommands.AsEnumerable()
            : _allCommands.Where(c => FuzzyMatch(c.Title, query) || FuzzyMatch(c.Category, query))
                          .OrderByDescending(c => FuzzyScore(c.Title, query))
                          .ThenBy(c => c.Title);

        foreach (var cmd in results)
            FilteredCommands.Add(cmd);

        SelectedCommand = FilteredCommands.FirstOrDefault();
    }

    /// <summary>
    /// Simple fuzzy match: checks if all characters of the query appear in
    /// the target string in order (subsequence match), case-insensitive.
    /// </summary>
    private static bool FuzzyMatch(string target, string query)
    {
        if (string.IsNullOrEmpty(target)) return false;
        var t = target.ToLowerInvariant();
        var q = query.ToLowerInvariant();
        int ti = 0, qi = 0;
        while (ti < t.Length && qi < q.Length)
        {
            if (t[ti] == q[qi]) qi++;
            ti++;
        }
        return qi == q.Length;
    }

    /// <summary>
    /// Score for ranking fuzzy matches: higher = better match.
    /// Rewards consecutive character matches and early matches.
    /// </summary>
    private static double FuzzyScore(string target, string query)
    {
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(query)) return 0;
        var t = target.ToLowerInvariant();
        var q = query.ToLowerInvariant();

        double score = 0;
        int ti = 0, qi = 0;
        int consecutive = 0;
        while (ti < t.Length && qi < q.Length)
        {
            if (t[ti] == q[qi])
            {
                score += 1.0 + consecutive * 0.5;
                consecutive++;
                if (ti == qi) score += 0.5; // early match bonus
                qi++;
            }
            else
            {
                consecutive = 0;
            }
            ti++;
        }
        return qi == q.Length ? score : 0;
    }

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedCommand?.Action is { } action)
        {
            _closeAction?.Invoke();
            action();
        }
    }

    [RelayCommand]
    private void SelectAndExecute(CommandItem? item)
    {
        if (item?.Action is { } action)
        {
            _closeAction?.Invoke();
            action();
        }
    }
}
