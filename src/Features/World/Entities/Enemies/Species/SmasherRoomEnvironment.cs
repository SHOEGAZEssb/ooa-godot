using System;

namespace oracleofages;

internal sealed record SmasherRoomEnvironment(
    Func<SmasherRoomEntity, SmasherCharacter?> SpawnParent,
    Action InitializeBossRoom,
    Action BeginMiniboss,
    Func<bool> InteractionSlotAvailable,
    Func<bool> PartSlotAvailable,
    Action DisableLinkCollisionsAndMenu,
    Action RestoreRoomMusic,
    Action<int> Sound,
    int KillableEnemyIndex,
    bool Counted,
    BossEntryMovement? Entry = null,
    Func<int>? FrameCounter = null);
