using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GaleTreeWarpDatabase
{
    private readonly Dictionary<(int Era, int Index), GaleTreeWarp> _entries = new();

    internal GaleTreeWarpDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/gale_tree_warps.tsv",
            new GeneratedTableSchema("gale tree warps", GeneratedTableKeySemantics.Unique,
                ["era", "index", "room", "position", "popup", "source"],
                ["era", "index"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _entries.Add((row.UnsignedDecimal(0), row.UnsignedDecimal(1)),
                new GaleTreeWarp(row.HexByte(2), row.HexByte(3), row.HexByte(4)));
        if (_entries.Count != 17) throw new InvalidOperationException("Expected 17 treeWarps.s entries, including terminators.");
    }

    internal GaleTreeWarp Get(RoomSession rooms, int index)
    {
        int era = (rooms.CurrentRoom.TilesetFlags >> 7) & 1;
        int offset = era == 0 && (rooms.SaveData.GetRoomFlags(0, 0xac) & 0x80) == 0 ? 1 : 0;
        return _entries[(era, (index & 7) + offset)];
    }

    internal int Move(RoomSession rooms, int index, int offset)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            index = (index + offset) & 7;
            GaleTreeWarp entry = Get(rooms, index);
            if (entry.Room != 0 && rooms.HasVisited(rooms.ActiveGroup, entry.Room)) return index;
        }
        throw new InvalidOperationException($"MENU_GALE_SEED has no visited tree in group ${rooms.ActiveGroup:x2}; treeWarps.s cannot select a destination.");
    }
}

internal readonly record struct GaleTreeWarp(int Room, int Position, int Popup);
