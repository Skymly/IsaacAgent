local MinimalMod = RegisterMod("MinimalMod", 1)

function MinimalMod:onGameStart(isSave)
    -- Fixture for FlaUI UI smoke (issue #80)
end

MinimalMod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    MinimalMod:onGameStart(isSave)
end)

print("MinimalMod loaded!")
