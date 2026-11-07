namespace Oracles;

internal sealed record MapleDroppedItemSpawn(
    MapleItemRecord Record,
    MapleEncounterState Encounter,
    int Slot,
    Vector2 SourcePosition,
    int SourceZFixed,
    bool UpdateThisFrame = false) : RoomEntitySpawn(UpdateThisFrame);
