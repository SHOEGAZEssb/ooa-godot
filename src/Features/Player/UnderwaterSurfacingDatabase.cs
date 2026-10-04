using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class UnderwaterSurfacingDatabase
{
    internal static UnderwaterSurfacingDatabase Shared { get; } = new();
    private readonly Dictionary<(int Group, int Room), int> _rooms = new();
    private readonly int[] _masks = new int[529];

    private UnderwaterSurfacingDatabase()
    {
        GeneratedTable masks = GeneratedTable.Load(
            "res://assets/oracle/metadata/underwater_surface_masks.tsv",
            new GeneratedTableSchema("underwater surfacing mask arena", GeneratedTableKeySemantics.Unique,
                ["index", "mask", "source"], ["index"], headerRequired: true));
        if (masks.Rows.Count != _masks.Length)
            throw new InvalidOperationException("underwaterSurfaceData.s: expected 529 contiguous surfacing words.");
        int index = 0;
        foreach (GeneratedTableRow row in masks.Rows)
        {
            if (row.Decimal(0) != index) throw row.Invalid(0, "contiguous source word order");
            _masks[index++] = row.HexWord(1);
        }
        GeneratedTable rooms = GeneratedTable.Load(
            "res://assets/oracle/metadata/underwater_surface_rooms.tsv",
            new GeneratedTableSchema("underwater surfacing rooms", GeneratedTableKeySemantics.Unique,
                ["group", "room", "mask-index", "source"], ["group", "room"], headerRequired: true));
        foreach (GeneratedTableRow row in rooms.Rows)
            _rooms.Add((row.Decimal(0, 0, 7), row.HexByte(1)), row.Decimal(2, 0, _masks.Length - 11));
        if (_rooms.Count != 219)
            throw new InvalidOperationException("underWaterSurfaceTable: expected 219 aliased room records.");
    }

    // code/ages/underwaterSurfacing.s. Bits are reversed across each row;
    // a set bit forbids surfacing. The first word's low two bits also select
    // the outdoor pollution branch, rather than describing playable tiles.
    internal bool CanSurface(int group, int room, int tilesetFlags, int packedPosition,
        bool pollutionFixed, int dungeonIndex, int jabuWaterLevel)
    {
        if ((tilesetFlags & (int)TilesetFlags.Underwater) == 0) return false;
        bool outdoors = (tilesetFlags & (int)TilesetFlags.Outdoors) != 0;
        if (!_rooms.TryGetValue((group, room), out int index)) return outdoors;
        if (outdoors)
        {
            int flags = _masks[index] & 3;
            if (flags != 0 && pollutionFixed)
            {
                if ((flags & 1) != 0) return true;
                index += 8;
            }
        }
        else if (dungeonIndex == 7 && (jabuWaterLevel & 3) == 2 && room is 0x4c or 0x4d)
            index += 11;
        index += (packedPosition >> 4) & 15;
        if ((uint)index >= (uint)_masks.Length)
            throw new NotSupportedException($"checkLinkCanSurface: room {group:x1}:{room:x2}, position ${packedPosition:x2} exceeds the imported mask arena.");
        return (_masks[index] & (1 << ((packedPosition & 15) ^ 15))) == 0;
    }
}
