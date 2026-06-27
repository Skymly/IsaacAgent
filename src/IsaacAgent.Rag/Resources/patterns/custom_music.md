---
title: Custom Music
category: pattern
tags: music, audio, sound, manager
---
# Custom Music

## Overview
This pattern shows how to add custom music tracks and override the game's
music playback using `MusicManager`. It covers playing custom tracks on
room/level entry, replacing boss music, and handling smooth transitions.

## main.lua

```lua
local mod = RegisterMod("Custom Music Mod", 1)

-- Custom music IDs are registered in resources/music.xml
-- The game assigns IDs sequentially after the vanilla tracks.
local MUSIC_CUSTOM_CALM = Isaac.GetMusicIdByName("Calm Caverns")
local MUSIC_CUSTOM_BOSS  = Isaac.GetMusicIdByName("Boss Fury")

-- Track the current custom track so we avoid restarting it every frame
local currentTrack = nil

-- Helper: crossfade to a new track
local function PlayMusicTrack(trackId, fadeMs)
    fadeMs = fadeMs or 500
    local music = MusicManager()
    if currentTrack == trackId then return end
    music:Fadeout(fadeMs)
    music:Queue(trackId, fadeMs)
    music:Play(trackId, fadeMs)
    currentTrack = trackId
end

-- Play custom calm music on every non-boss room
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    local roomType = room:GetType()

    if roomType == RoomType.ROOM_BOSS then
        -- Replace boss music with our custom boss track
        PlayMusicTrack(MUSIC_CUSTOM_BOSS, 800)
    elseif roomType == RoomType.ROOM_DEFAULT
        or roomType == RoomType.ROOM_TREASURE then
        PlayMusicTrack(MUSIC_CUSTOM_CALM, 500)
    end
end)

-- Play a special track when entering a new floor
mod:AddCallback(ModCallbacks.MC_POST_NEW_LEVEL, function()
    local level = Game():GetLevel()
    local stage = level:GetStage()

    if stage == LevelStage.STAGE1_1 then
        -- Intro floor music
        PlayMusicTrack(MUSIC_CUSTOM_CALM, 1000)
    end
end)

-- Pause music during a stall (e.g. boss intro animation)
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    if room:GetType() == RoomType.ROOM_BOSS then
        -- Briefly pause music for dramatic effect
        MusicManager():Pause()
        Isaac.SetTimeout(120, function()
            MusicManager():Resume()
            PlayMusicTrack(MUSIC_CUSTOM_BOSS, 800)
        end)
    end
end)

-- Restore vanilla music when leaving to the menu
mod:AddCallback(ModCallbacks.MC_PRE_GAME_EXIT, function(_, shouldSave)
    MusicManager():Stop()
    currentTrack = nil
end)

-- Lower music volume during low health tension
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    if player:GetHearts() <= 1 then
        MusicManager():UpdateVolume(0.4)
    else
        MusicManager():UpdateVolume(1.0)
    end
end)
```

## music.xml

```xml
<music>
  <track name="Calm Caverns" file="music/calm_caverns.ogg" loop="true"/>
  <track name="Boss Fury"     file="music/boss_fury.ogg"    loop="true"/>
</music>
```

## Key Points
- Use `Isaac.GetMusicIdByName()` to resolve custom track IDs registered in `music.xml`
- `MusicManager()` returns the singleton music manager
- `music:Play(id, fade)` plays a track with an optional fade-in
- `music:Fadeout(ms)` fades out the current track before switching
- `music:Queue(id, ms)` queues a track to play after the current one
- `MC_POST_NEW_ROOM` — best hook for per-room music changes
- `MC_POST_NEW_LEVEL` — best hook for per-floor music changes
- `music:Pause()` / `music:Resume()` for cutscene-style stalls
- `music:UpdateVolume(vol)` adjusts music volume (0.0–1.0)
- Custom music files go in `resources/music/` as `.ogg` files
