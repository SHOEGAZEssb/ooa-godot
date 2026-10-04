using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Ordered replaceJabuTilesIfUnderwater room-load substitutions.</summary>
internal sealed class JabuWaterTileDatabase
{
    private readonly Dictionary<byte, byte> _substitutions = new();

    internal JabuWaterTileDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/jabu_water_tile_substitutions.tsv",
            new GeneratedTableSchema("Jabu water tile substitutions", GeneratedTableKeySemantics.Unique,
                ["index", "replacement", "original", "source"], ["index"], headerRequired: true));
        int index = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.Decimal(0, 0, 8) != index++)
                throw row.Invalid(0, "the next source-ordered substitution index $00-$08");
            if (!_substitutions.TryAdd((byte)row.HexByte(2), (byte)row.HexByte(1)))
                throw row.Invalid(2, "a distinct source tile in replaceJabuTilesIfUnderwater");
            row.RequiredString(3);
        }
        if (index != 9)
            throw new InvalidOperationException("tileSubstitutions.s:replaceJabuTilesIfUnderwater requires nine replacement pairs.");
    }

    internal void Apply(int group, int dungeon, int? floor, OracleRoomData room,
        OracleSaveData save, long animationTick)
    {
        // applyAllTileSubstitutions dispatches this pass only for groups $04/$05.
        // Despite the routine's name, equality selects the first dry floor.
        if ((group & 6) != 4 || dungeon != 7 ||
            (room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0) return;
        if (!floor.HasValue)
            throw new InvalidOperationException($"tileSubstitutions.s:replaceJabuTilesIfUnderwater lacks the native floor for room {group:x1}:{room.Id:x2}.");
        if ((save.ReadWramByte(WramAddress.wJabuWaterLevel) & 7) == floor.Value)
            room.ApplyMetatileSubstitutions(_substitutions, animationTick);
    }
}
