using Godot;
using System;

namespace oracleofages;

// companionCheckCanSpawn state $00 yields once so interaction initializers
// can mark solid positions. Rejection leaves remembered bytes untouched.
internal sealed class CompanionRoomInitialization(OracleRoomData room, OracleSaveData saveData,
    Func<Vector2, bool> occupied)
{
    private int _updates;
    internal bool Pending => _updates < 2;
    internal bool Rejected { get; private set; }
    internal bool Advance(ref Vector2 position)
    {
        if (++_updates == 1) return false;
        if (occupied(position)) { Rejected = true; return true; }
        int collision = room.GetTerrainInfo(position + new Vector2(0, 5)).Collision;
        if (collision is 0x0f or 0x10)
        {
            position = CompanionRuntimeState.ReadLastAnimalMountPosition(saveData);
            Rejected = room.GetTerrainInfo(position).Collision != 0;
        }
        return true;
    }
}
