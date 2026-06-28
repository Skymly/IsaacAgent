using IsaacAgent.Agent.Engine;
using IsaacAgent.Agent.Skills;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class SkillRegistryTests
{
    [Fact]
    public void Register_AddsSkillToRegistry()
    {
        var registry = new SkillRegistry();
        registry.Register(new CreateCollectibleSkill());

        Assert.Single(registry.All);
        Assert.Equal("create-collectible", registry.All[0].Name);
    }

    [Fact]
    public void RegisterAll_AddsMultipleSkills()
    {
        var registry = new SkillRegistry();
        registry.RegisterAll([
            new CreateCollectibleSkill(),
            new DebugFromLogSkill(),
            new ValidateProjectSkill()
        ]);

        Assert.Equal(3, registry.All.Count);
    }

    [Fact]
    public void FindBySlashCommand_ReturnsMatchingSkill()
    {
        var registry = new SkillRegistry();
        registry.Register(new CreateCollectibleSkill());

        var skill = registry.FindBySlashCommand("/create-item");

        Assert.NotNull(skill);
        Assert.Equal("create-collectible", skill.Name);
    }

    [Fact]
    public void FindBySlashCommand_ReturnsNullForUnknownCommand()
    {
        var registry = new SkillRegistry();
        registry.Register(new CreateCollectibleSkill());

        var skill = registry.FindBySlashCommand("/unknown");

        Assert.Null(skill);
    }

    [Fact]
    public void FindBySlashCommand_IsCaseInsensitive()
    {
        var registry = new SkillRegistry();
        registry.Register(new DebugFromLogSkill());

        var skill = registry.FindBySlashCommand("/DEBUG");

        Assert.NotNull(skill);
        Assert.Equal("debug-from-log", skill.Name);
    }

    [Fact]
    public void GetDescriptors_ReturnsAllSkillDescriptors()
    {
        var registry = new SkillRegistry();
        registry.RegisterAll([
            new CreateCollectibleSkill(),
            new ValidateProjectSkill()
        ]);

        var descriptors = registry.GetDescriptors();

        Assert.Equal(2, descriptors.Count);
        Assert.Contains(descriptors, d => d.Name == "create-collectible");
        Assert.Contains(descriptors, d => d.Name == "validate-project");
    }
}

public class CreateCollectibleSkillTests
{
    [Fact]
    public void ShouldActivate_CreatePassiveItem_ReturnsTrue()
    {
        var skill = new CreateCollectibleSkill();
        Assert.True(skill.ShouldActivate("Create a passive item that doubles fire rate", null));
    }

    [Fact]
    public void ShouldActivate_CreateActiveItem_ReturnsTrue()
    {
        var skill = new CreateCollectibleSkill();
        Assert.True(skill.ShouldActivate("Make an active item that shoots lasers", null));
    }

    [Fact]
    public void ShouldActivate_AddCollectible_ReturnsTrue()
    {
        var skill = new CreateCollectibleSkill();
        Assert.True(skill.ShouldActivate("Add a collectible to my mod", null));
    }

    [Fact]
    public void ShouldActivate_ItemPoolModification_ReturnsFalse()
    {
        var skill = new CreateCollectibleSkill();
        Assert.False(skill.ShouldActivate("Modify the item pool to remove red hearts", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new CreateCollectibleSkill();
        Assert.False(skill.ShouldActivate("/create-item a lucky charm", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new CreateCollectibleSkill();
        Assert.False(skill.ShouldActivate("What is the MC_EVALUATE_CACHE callback?", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new CreateCollectibleSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Create Custom Collectible", augment);
        Assert.Contains("get_pattern", augment);
        Assert.Contains("validate_xml", augment);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new CreateCollectibleSkill();
        var result = await skill.PreFetchContextAsync("create a passive item", null);
        Assert.Empty(result);
    }

    [Fact]
    public async Task PreFetchContextAsync_NotReadyRetriever_ReturnsEmpty()
    {
        var mockRetriever = new Mock<IRetriever>();
        mockRetriever.Setup(r => r.IsReady).Returns(false);

        var skill = new CreateCollectibleSkill();
        var result = await skill.PreFetchContextAsync("create a passive item", mockRetriever.Object);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PreFetchContextAsync_ReadyRetriever_ReturnsContextMessages()
    {
        var mockRetriever = new Mock<IRetriever>();
        mockRetriever.Setup(r => r.IsReady).Returns(true);
        mockRetriever.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RetrievalResult { Chunk = new KnowledgeChunk { Id = "1", Source = "test", Category = "pattern", Title = "Passive Item", Content = "local mod = RegisterMod(...)" }, Score = 0.9f }
            ]);

        var skill = new CreateCollectibleSkill();
        var result = await skill.PreFetchContextAsync("create a passive item", mockRetriever.Object);

        Assert.Single(result);
        Assert.Equal("system", result[0].Role);
        Assert.Contains("Pre-fetched Pattern", result[0].Content);
    }

    [Fact]
    public void SlashCommand_IsCreateItem()
    {
        var skill = new CreateCollectibleSkill();
        Assert.Equal("/create-item", skill.SlashCommand);
    }
}

public class DebugFromLogSkillTests
{
    [Fact]
    public void ShouldActivate_CrashMessage_ReturnsTrue()
    {
        var skill = new DebugFromLogSkill();
        Assert.True(skill.ShouldActivate("My mod is crashing when I pick up an item", null));
    }

    [Fact]
    public void ShouldActivate_ErrorMessage_ReturnsTrue()
    {
        var skill = new DebugFromLogSkill();
        Assert.True(skill.ShouldActivate("I'm getting an error in main.lua", null));
    }

    [Fact]
    public void ShouldActivate_NotWorkingMessage_ReturnsTrue()
    {
        var skill = new DebugFromLogSkill();
        Assert.True(skill.ShouldActivate("My familiar is not working correctly", null));
    }

    [Fact]
    public void ShouldActivate_FixMessage_ReturnsTrue()
    {
        var skill = new DebugFromLogSkill();
        Assert.True(skill.ShouldActivate("Can you fix the bug in my callback?", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new DebugFromLogSkill();
        Assert.False(skill.ShouldActivate("Create a new boss with 3 phases", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new DebugFromLogSkill();
        Assert.False(skill.ShouldActivate("/debug my mod crashes", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsDiagnosticSteps()
    {
        var skill = new DebugFromLogSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Debug From Log", augment);
        Assert.Contains("parse_log", augment);
        Assert.Contains("diagnose_lua", augment);
    }

    [Fact]
    public void SlashCommand_IsDebug()
    {
        var skill = new DebugFromLogSkill();
        Assert.Equal("/debug", skill.SlashCommand);
    }
}

public class ValidateProjectSkillTests
{
    [Fact]
    public void ShouldActivate_ValidateProject_ReturnsTrue()
    {
        var skill = new ValidateProjectSkill();
        Assert.True(skill.ShouldActivate("Validate my project before testing", null));
    }

    [Fact]
    public void ShouldActivate_CheckMod_ReturnsTrue()
    {
        var skill = new ValidateProjectSkill();
        Assert.True(skill.ShouldActivate("Check my mod for issues", null));
    }

    [Fact]
    public void ShouldActivate_VerifyEverything_ReturnsTrue()
    {
        var skill = new ValidateProjectSkill();
        Assert.True(skill.ShouldActivate("Verify all files in my mod", null));
    }

    [Fact]
    public void ShouldActivate_ValidateWithoutProjectScope_ReturnsFalse()
    {
        var skill = new ValidateProjectSkill();
        Assert.False(skill.ShouldActivate("Validate this callback", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new ValidateProjectSkill();
        Assert.False(skill.ShouldActivate("Create a custom item", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new ValidateProjectSkill();
        Assert.False(skill.ShouldActivate("/validate", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsValidationSteps()
    {
        var skill = new ValidateProjectSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Validate Project", augment);
        Assert.Contains("validate_xml", augment);
        Assert.Contains("diagnose_lua", augment);
        Assert.Contains("list_files", augment);
    }

    [Fact]
    public async Task PreFetchContextAsync_ReturnsEmpty()
    {
        var skill = new ValidateProjectSkill();
        var result = await skill.PreFetchContextAsync("validate project", null);
        Assert.Empty(result);
    }

    [Fact]
    public void SlashCommand_IsValidate()
    {
        var skill = new ValidateProjectSkill();
        Assert.Equal("/validate", skill.SlashCommand);
    }
}

public class CreateFamiliarSkillTests
{
    [Fact]
    public void ShouldActivate_CreateFamiliar_ReturnsTrue()
    {
        var skill = new CreateFamiliarSkill();
        Assert.True(skill.ShouldActivate("Create a familiar that orbits the player", null));
    }

    [Fact]
    public void ShouldActivate_AddCompanion_ReturnsTrue()
    {
        var skill = new CreateFamiliarSkill();
        Assert.True(skill.ShouldActivate("Add a companion that follows me", null));
    }

    [Fact]
    public void ShouldActivate_MakeShootingFamiliar_ReturnsTrue()
    {
        var skill = new CreateFamiliarSkill();
        Assert.True(skill.ShouldActivate("Make a shooting familiar", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new CreateFamiliarSkill();
        Assert.False(skill.ShouldActivate("Create a custom item", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new CreateFamiliarSkill();
        Assert.False(skill.ShouldActivate("/create-familiar an orbiting one", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new CreateFamiliarSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Create Custom Familiar", augment);
        Assert.Contains("MC_FAMILIAR_INIT", augment);
        Assert.Contains("entities2.xml", augment);
    }

    [Fact]
    public void SlashCommand_IsCreateFamiliar()
    {
        var skill = new CreateFamiliarSkill();
        Assert.Equal("/create-familiar", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new CreateFamiliarSkill();
        var result = await skill.PreFetchContextAsync("create a familiar", null);
        Assert.Empty(result);
    }
}

public class AddCallbackSkillTests
{
    [Fact]
    public void ShouldActivate_AddCallback_ReturnsTrue()
    {
        var skill = new AddCallbackSkill();
        Assert.True(skill.ShouldActivate("Add a callback for when player takes damage", null));
    }

    [Fact]
    public void ShouldActivate_RegisterCallback_ReturnsTrue()
    {
        var skill = new AddCallbackSkill();
        Assert.True(skill.ShouldActivate("Register callback MC_POST_NEW_ROOM", null));
    }

    [Fact]
    public void ShouldActivate_HookInto_ReturnsTrue()
    {
        var skill = new AddCallbackSkill();
        Assert.True(skill.ShouldActivate("Hook into the MC_EVALUATE_CACHE callback", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new AddCallbackSkill();
        Assert.False(skill.ShouldActivate("Create a new item", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new AddCallbackSkill();
        Assert.False(skill.ShouldActivate("/add-callback MC_POST_UPDATE", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new AddCallbackSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Add Callback", augment);
        Assert.Contains("get_callback_info", augment);
        Assert.Contains("diff_apply", augment);
    }

    [Fact]
    public void SlashCommand_IsAddCallback()
    {
        var skill = new AddCallbackSkill();
        Assert.Equal("/add-callback", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_WithCallbackName_PreFetchesInfo()
    {
        var mockRetriever = new Mock<IRetriever>();
        mockRetriever.Setup(r => r.IsReady).Returns(true);
        mockRetriever.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RetrievalResult { Chunk = new KnowledgeChunk { Id = "1", Source = "test", Category = "callback", Title = "MC_POST_UPDATE", Content = "Called every frame" }, Score = 0.9f }
            ]);

        var skill = new AddCallbackSkill();
        var result = await skill.PreFetchContextAsync("add callback MC_POST_UPDATE to main.lua", mockRetriever.Object);

        Assert.Single(result);
        Assert.Contains("MC_POST_UPDATE", result[0].Content);
    }

    [Fact]
    public async Task PreFetchContextAsync_WithoutCallbackName_ReturnsEmpty()
    {
        var mockRetriever = new Mock<IRetriever>();
        mockRetriever.Setup(r => r.IsReady).Returns(true);

        var skill = new AddCallbackSkill();
        var result = await skill.PreFetchContextAsync("add a callback for player damage", mockRetriever.Object);

        Assert.Empty(result);
    }
}

public class AddSaveDataSkillTests
{
    [Fact]
    public void ShouldActivate_AddSaveData_ReturnsTrue()
    {
        var skill = new AddSaveDataSkill();
        Assert.True(skill.ShouldActivate("Add save data to track total runs", null));
    }

    [Fact]
    public void ShouldActivate_ImplementPersistence_ReturnsTrue()
    {
        var skill = new AddSaveDataSkill();
        Assert.True(skill.ShouldActivate("Implement persistence across runs", null));
    }

    [Fact]
    public void ShouldActivate_EnableSaveFile_ReturnsTrue()
    {
        var skill = new AddSaveDataSkill();
        Assert.True(skill.ShouldActivate("Enable save file for my mod settings", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new AddSaveDataSkill();
        Assert.False(skill.ShouldActivate("Create a custom boss", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new AddSaveDataSkill();
        Assert.False(skill.ShouldActivate("/add-save-data to track wins", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new AddSaveDataSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Add Save Data", augment);
        Assert.Contains("SaveModData", augment);
        Assert.Contains("MC_POST_GAME_STARTED", augment);
        Assert.Contains("MC_PRE_GAME_EXIT", augment);
    }

    [Fact]
    public void SlashCommand_IsAddSaveData()
    {
        var skill = new AddSaveDataSkill();
        Assert.Equal("/add-save-data", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new AddSaveDataSkill();
        var result = await skill.PreFetchContextAsync("add save data", null);
        Assert.Empty(result);
    }
}

public class AddTrinketSkillTests
{
    [Fact]
    public void ShouldActivate_AddTrinket_ReturnsTrue()
    {
        var skill = new AddTrinketSkill();
        Assert.True(skill.ShouldActivate("Add a trinket that gives luck", null));
    }

    [Fact]
    public void ShouldActivate_CreateTrinket_ReturnsTrue()
    {
        var skill = new AddTrinketSkill();
        Assert.True(skill.ShouldActivate("Create a pocket active trinket", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new AddTrinketSkill();
        Assert.False(skill.ShouldActivate("Create a custom item", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new AddTrinketSkill();
        Assert.False(skill.ShouldActivate("/add-trinket luck boost", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new AddTrinketSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Add Custom Trinket", augment);
        Assert.Contains("items.xml", augment);
        Assert.Contains("MC_POST_TRINKET_INIT", augment);
    }

    [Fact]
    public void SlashCommand_IsAddTrinket()
    {
        var skill = new AddTrinketSkill();
        Assert.Equal("/add-trinket", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new AddTrinketSkill();
        var result = await skill.PreFetchContextAsync("add a trinket", null);
        Assert.Empty(result);
    }
}

public class AddCardSkillTests
{
    [Fact]
    public void ShouldActivate_AddCard_ReturnsTrue()
    {
        var skill = new AddCardSkill();
        Assert.True(skill.ShouldActivate("Add a card that teleports the player", null));
    }

    [Fact]
    public void ShouldActivate_CreateRune_ReturnsTrue()
    {
        var skill = new AddCardSkill();
        Assert.True(skill.ShouldActivate("Create a rune that buffs damage", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new AddCardSkill();
        Assert.False(skill.ShouldActivate("Create a custom trinket", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new AddCardSkill();
        Assert.False(skill.ShouldActivate("/add-card teleport", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new AddCardSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Add Custom Card", augment);
        Assert.Contains("MC_USE_CARD", augment);
        Assert.Contains("items.xml", augment);
    }

    [Fact]
    public void SlashCommand_IsAddCard()
    {
        var skill = new AddCardSkill();
        Assert.Equal("/add-card", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new AddCardSkill();
        var result = await skill.PreFetchContextAsync("add a card", null);
        Assert.Empty(result);
    }
}

public class AddPillSkillTests
{
    [Fact]
    public void ShouldActivate_AddPill_ReturnsTrue()
    {
        var skill = new AddPillSkill();
        Assert.True(skill.ShouldActivate("Add a pill effect that heals", null));
    }

    [Fact]
    public void ShouldActivate_CreatePill_ReturnsTrue()
    {
        var skill = new AddPillSkill();
        Assert.True(skill.ShouldActivate("Create a horse pill that poisons enemies", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new AddPillSkill();
        Assert.False(skill.ShouldActivate("Create a custom card", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new AddPillSkill();
        Assert.False(skill.ShouldActivate("/add-pill heal", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new AddPillSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Add Custom Pill", augment);
        Assert.Contains("MC_USE_PILL", augment);
        Assert.Contains("pills.xml", augment);
    }

    [Fact]
    public void SlashCommand_IsAddPill()
    {
        var skill = new AddPillSkill();
        Assert.Equal("/add-pill", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new AddPillSkill();
        var result = await skill.PreFetchContextAsync("add a pill", null);
        Assert.Empty(result);
    }
}

public class AddBossSkillTests
{
    [Fact]
    public void ShouldActivate_AddBoss_ReturnsTrue()
    {
        var skill = new AddBossSkill();
        Assert.True(skill.ShouldActivate("Add a boss that shoots lasers", null));
    }

    [Fact]
    public void ShouldActivate_CreateBoss_ReturnsTrue()
    {
        var skill = new AddBossSkill();
        Assert.True(skill.ShouldActivate("Create a custom boss fight with 3 phases", null));
    }

    [Fact]
    public void ShouldActivate_UnrelatedMessage_ReturnsFalse()
    {
        var skill = new AddBossSkill();
        Assert.False(skill.ShouldActivate("Create a custom familiar", null));
    }

    [Fact]
    public void ShouldActivate_SlashCommand_ReturnsFalse()
    {
        var skill = new AddBossSkill();
        Assert.False(skill.ShouldActivate("/add-boss laser boss", null));
    }

    [Fact]
    public void GetPromptAugmentation_ReturnsWorkflowSteps()
    {
        var skill = new AddBossSkill();
        var augment = skill.GetPromptAugmentation();

        Assert.NotNull(augment);
        Assert.Contains("Add Custom Boss", augment);
        Assert.Contains("MC_NPC_UPDATE", augment);
        Assert.Contains("entities2.xml", augment);
    }

    [Fact]
    public void SlashCommand_IsAddBoss()
    {
        var skill = new AddBossSkill();
        Assert.Equal("/add-boss", skill.SlashCommand);
    }

    [Fact]
    public async Task PreFetchContextAsync_NullRetriever_ReturnsEmpty()
    {
        var skill = new AddBossSkill();
        var result = await skill.PreFetchContextAsync("add a boss", null);
        Assert.Empty(result);
    }
}
