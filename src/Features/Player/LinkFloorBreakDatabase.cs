using System;

namespace Oracles;

internal sealed class LinkFloorBreakDatabase
{
    internal LinkFloorBreakRecord Floor { get; }
    internal LinkFloorBreakRecord? Ice { get; }

    internal LinkFloorBreakDatabase(string assetRoot)
    {
        bool seasons = OracleAssetPack.GameFor(assetRoot) == OracleGame.Seasons;
        var table = GeneratedTable.Load(assetRoot + "metadata/link_floor_break.tsv",
            new("native Link floor breaking",GeneratedTableKeySemantics.Unique,
                ["kind","updates","replacement","source"],["kind"],headerRequired:true));
        if (table.Rows.Count != (seasons ? 2 : 1))
            throw new InvalidOperationException($"{table.Path}: missing native floor/ice handler operands.");
        for (int index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            if (row.RequiredString(0) != (index == 0 ? "floor" : "ice")) throw row.Invalid(0,"native floor/ice handler order");
            var record = new LinkFloorBreakRecord(row.Decimal(1,1,255),(byte)row.HexByte(2));
            if (record.Updates != 32 || record.Replacement != (index == 0 ? 0xf3 : 0xfd))
                throw row.Invalid(1,"clean-US floor/ice threshold and replacement operands");
            _ = row.RequiredString(3);
            if (index == 0) Floor = record;
            else Ice = record;
        }
    }
}

internal readonly record struct LinkFloorBreakRecord(int Updates, byte Replacement);
