using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct BreakableTileRecord(int ActiveCollisions, int Tile, int Mode, int SourceMask, int Drop, int Effect, int Replacement, int RoomFlagAction, int GashaMaturity)
{
    public bool AllowsSource(int source) => (SourceMask & (1 << source)) != 0;
    public void ApplyPersistentEffects(OracleSaveData? saveData, int group, int room, Func<Vector2I, int?>? linkedRoomNeighbor = null)
    {
        if ((Effect & 0x80) == 0)
            return;
        if (saveData is null)
        {
            throw new InvalidOperationException($"Breakable tile ${Tile:x2} requires live room-flag state.");
        }

        if (GashaMaturity != 0)
            saveData.AddGashaMaturity(GashaMaturity);
        if (RoomFlagAction == 0xff)
            return;
        int linkKind = RoomFlagAction & 0xc0;
        if (linkKind == 0)
        {
            saveData.SetRoomFlag(group, room, (byte)(1 << (RoomFlagAction & 0x0f)));
            return;
        }

        if (linkKind is not (0x40 or 0x80))
        {
            throw new InvalidOperationException($"Breakable tile ${Tile:x2} uses invalid linked room-flag " + $"action ${RoomFlagAction:x2}.");
        }

        int directionCode = RoomFlagAction & 0x0f;
        (Vector2I direction, byte roomFlag, byte oppositeRoomFlag) = directionCode switch
        {
            0x00 => (Vector2I.Up, (byte)0x01, (byte)0x04),
            0x04 => (Vector2I.Right, (byte)0x02, (byte)0x08),
            0x08 => (Vector2I.Down, (byte)0x04, (byte)0x01),
            0x0c => (Vector2I.Left, (byte)0x08, (byte)0x02),
            _ => throw new InvalidOperationException($"Breakable tile ${Tile:x2} uses invalid linked direction " + $"${directionCode:x2}.")};
        if (linkKind == 0x40 && direction.Y != 0)
        {
            throw new InvalidOperationException($"Indoor linked breakable tile ${Tile:x2} uses unsupported " + $"vertical action ${RoomFlagAction:x2}.");
        }

        int neighbor = linkedRoomNeighbor?.Invoke(direction) ?? throw new InvalidOperationException($"Breakable tile ${Tile:x2} in room {group:x1}:{room:x2} " + $"requires a linked-room neighbor for action ${RoomFlagAction:x2}.");
        saveData.SetRoomFlag(group, room, roomFlag);
        saveData.SetRoomFlag(group, neighbor, oppositeRoomFlag);
    }
}
