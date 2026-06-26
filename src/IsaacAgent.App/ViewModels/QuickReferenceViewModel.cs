using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IsaacAgent.Core.Knowledge;

namespace IsaacAgent.App.ViewModels;

public sealed partial class QuickReferenceViewModel : ObservableObject
{
    public ObservableCollection<string> Callbacks { get; } = [];
    public ObservableCollection<string> Classes { get; } = [];
    public ObservableCollection<string> ModStructure { get; } = [];

    public QuickReferenceViewModel()
    {
        LoadCallbacks();
        LoadClasses();
        LoadModStructure();
    }

    private void LoadCallbacks()
    {
        var commonCallbacks = new[]
        {
            "MC_POST_GAME_STARTED", "MC_POST_UPDATE", "MC_USE_ITEM",
            "MC_POST_NEW_ROOM", "MC_POST_PLAYER_UPDATE", "MC_POST_NEW_LEVEL",
            "MC_POST_PEFFECT_UPDATE", "MC_ENTITY_TAKE_DMG", "MC_PRE_USE_ITEM",
            "MC_POST_PICKUP_UPDATE", "MC_NPC_UPDATE", "MC_POST_RENDER"
        };

        foreach (var cb in commonCallbacks)
        {
            if (ModCallbacks.Callbacks.ContainsKey(cb))
                Callbacks.Add(cb);
        }

        // Add any remaining vanilla callbacks (sorted)
        AddSortedKeys(ModCallbacks.Callbacks, Callbacks);

        // Add REPENTOGON-only callbacks (sorted, prefixed for clarity)
        AddSortedKeys(ModCallbacks.RepentogonCallbacks, Callbacks);
    }

    private void LoadClasses()
    {
        AddSortedKeys(IsaacClasses.Classes, Classes);
    }

    /// <summary>
    /// Adds keys from <paramref name="source"/> to <paramref name="target"/>,
    /// skipping any that already exist, in sorted order.
    /// </summary>
    private static void AddSortedKeys<T>(Dictionary<string, T> source, ICollection<string> target)
    {
        foreach (var key in source.Keys.OrderBy(k => k))
        {
            if (!target.Contains(key))
                target.Add(key);
        }
    }

    private void LoadModStructure()
    {
        ModStructure.Add("main.lua");
        ModStructure.Add("metadata.xml");
        ModStructure.Add("items.xml");
        ModStructure.Add("entities2.xml");
        ModStructure.Add("trinkets.xml");
        ModStructure.Add("players.xml");
        ModStructure.Add("resources/");
    }
}
