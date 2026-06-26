using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;

namespace IsaacAgent.App.ViewModels;

/// <summary>
/// View model for the Visual Diff Viewer window.
/// </summary>
public sealed partial class DiffViewerViewModel : ObservableObject
{
    private readonly DiffService _diffService;
    private string? _projectDir;

    [ObservableProperty]
    private DiffFile? _selectedFile;

    public DiffService DiffService => _diffService;

    public DiffViewerViewModel(DiffService diffService)
    {
        _diffService = diffService;
    }

    /// <summary>
    /// Set the project directory to diff against.
    /// </summary>
    public void SetProjectDir(string? projectDir)
    {
        _projectDir = projectDir;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_projectDir))
        {
            _diffService.StatusText = "No project open";
            return;
        }

        await _diffService.LoadDiffAsync(_projectDir);
        SelectedFile = _diffService.Files.FirstOrDefault();
    }

    partial void OnSelectedFileChanged(DiffFile? value)
    {
        // Selection changed — the view binds to SelectedFile.Lines directly
    }
}
