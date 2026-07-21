namespace IsaacAgent.App.Services;

/// <summary>No-op Settings apply for unit tests that do not exercise Save.</summary>
public sealed class NoOpSettingsApply : ISettingsApply
{
    public static NoOpSettingsApply Instance { get; } = new();

    public void Apply(ProviderIntent intent, ISettingsApplyProgress progress)
    {
    }
}
