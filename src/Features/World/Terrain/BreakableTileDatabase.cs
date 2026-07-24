using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class BreakableTileDatabase
{
    public const int SourceBracelet = 0x00;
    public const int SourceSwordLevel1 = 0x01;
    public const int SourceSwordLevel2 = 0x02;
    public const int SourceExpertsRing = 0x03;
    public const int SourceLanded = 0x05;
    public const int SourceShovel = 0x06;
    public const int SourceEmberSeed = 0x0c;

    private readonly Dictionary<int, BreakableTileRecord> _records = new();

    public BreakableTileDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/breakable_tiles.tsv",
            new GeneratedTableSchema(
                "breakable tiles",
                GeneratedTableKeySemantics.Unique,
                [
                    "active-collisions", "tile", "mode", "source-mask", "drop",
                    "effect", "replacement", "room-flag-action", "gasha-maturity"
                ],
                ["active-collisions", "tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int activeCollisions = row.Decimal(0, 0, 0xff);
            int tile = row.HexByte(1);
            BreakableTileRecord record = new BreakableTileRecord(
                activeCollisions,
                tile,
                row.HexByte(2),
                row.HexInt(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                row.UnsignedDecimal(8));
            _records.Add((activeCollisions << 8) | tile, record);
        }
    }

    public bool TryGet(int activeCollisions, int tile, out BreakableTileRecord record) =>
        _records.TryGetValue((activeCollisions << 8) | tile, out record);
}

public readonly record struct BreakableTileRecord(int ActiveCollisions, int Tile, int Mode, int SourceMask, int Drop, int Effect, int Replacement, int RoomFlagAction, int GashaMaturity)
{
    public bool AllowsSource(int source) => (SourceMask & (1 << source)) != 0;

    public byte ReplacementFor(OracleRoomData room, Vector2 tilePoint)
    {
        bool useOriginalLayout =
            Tile == 0xdb ||
            (Tile == 0x10 && room.ActiveCollisions is 1 or 2);
        if (useOriginalLayout)
        {
            byte original = room.GetOriginalMetatile(tilePoint);
            byte collision = room.GetCollision(original);
            if (collision == 0 || collision >= 0x10)
                return original;
        }

        return (byte)Replacement;
    }

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
