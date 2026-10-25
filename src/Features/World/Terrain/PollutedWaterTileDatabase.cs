using System;
using System.Collections.Generic;

namespace Oracles;

// tileSubstitutions.s:replacePollutionWithWaterIfPollutionFixed, before single-tile changes.
internal sealed class PollutedWaterTileDatabase
{
    private readonly Dictionary<int, Dictionary<byte, byte>> _substitutions = new();

    internal PollutedWaterTileDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "metadata/polluted_water_tile_substitutions.tsv",
            new GeneratedTableSchema("polluted water tile substitutions", GeneratedTableKeySemantics.Unique,
                ["underwater-mask", "replacement", "original", "source"], ["underwater-mask"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int mask = row.HexByte(0);
            if (mask is not (0 or (int)TilesetFlags.Underwater))
                throw row.Invalid(0, "the native underwater mask $00 or $40");
            _substitutions.Add(mask, new() { [(byte)row.HexByte(2)] = (byte)row.HexByte(1) });
            row.RequiredString(3);
        }
        if (_substitutions.Count != 2)
            throw new InvalidOperationException("tileSubstitutions.s:replacePollutionWithWaterIfPollutionFixed requires both water replacement pairs.");
    }

    internal void Apply(OracleRoomData room, OracleSaveData save, long animationTick)
    {
        if (!save.HasGlobalFlag(AgesGlobalFlag.WaterPollutionFixed) ||
            (room.TilesetFlags & (int)TilesetFlags.Outdoors) == 0) return;
        room.ApplyMetatileSubstitutions(
            _substitutions[room.TilesetFlags & (int)TilesetFlags.Underwater], animationTick);
    }
}
