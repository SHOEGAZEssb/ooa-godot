using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class SeasonsRoomContent(SeasonsOpeningData data) : IRoomContent
{
    public string AssetRoot => SeasonsGameContent.AssetRoot;
    public DungeonMapDatabase? DungeonMaps => null;
    public DungeonKeyDoorDatabase? KeyDoors => null;
    // bank1.s:checkWarpsTopDown adds $05 before admitting the next room.
    public int RoomLoadMaturity => 5;

    public OracleRoomData LoadRoom(int group, int room, OracleSaveData save,
        OracleRuntimeState memory, long animationTick, byte toggleState)
    {
        RequireOverworld(group, room);
        OracleRoomData loaded = data.LoadRoom(room, memory.ReadField("wRoomStateModifier"));
        loaded.ResetAndApplyRoomInitializationChanges(new Dictionary<int, byte>(), animationTick);
        return loaded;
    }

    public void ActivateRoom(OracleRoomData room) => data.ActivateRoom(room);
    public int GetDungeonIndex(int group, int room)
    {
        RequireOverworld(group, room);
        return -1;
    }

    public byte ShopFlags(int group, int room)
    {
        RequireOverworld(group, room);
        return 0;
    }

    private static void RequireOverworld(int group, int room)
    {
        if (group != 0)
            throw new NotSupportedException($"Seasons room ${group:x1}:${room:x2}: only the imported opening overworld is bound.");
    }
}
