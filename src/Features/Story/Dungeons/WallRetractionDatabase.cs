using System;

namespace oracleofages;

internal sealed class WallRetractionDatabase
{
    internal int InitialWait { get; }
    internal int SecondWait { get; }
    internal int Shake { get; }
    internal int Interval { get; }
    internal int Rectangle { get; }
    internal int Steps { get; }
    internal byte FinalCollision { get; }
    internal byte[] Map { get; }
    internal byte[] Attributes { get; }
    internal byte[] LayoutScratch { get; }
    internal string Source { get; }
    internal WallRetractionDatabase()
    {
        var row=GeneratedTable.Load("res://assets/oracle/objects/mermaid_wall_retraction.tsv",
            new GeneratedTableSchema("Native wall retraction",GeneratedTableKeySemantics.Ordered,
                ["initial-wait","second-wait","shake","interval","rectangle","steps","final-collision","source"],headerRequired:true)).SingleRow();
        InitialWait=row.HexByte(0); SecondWait=row.HexByte(1); Shake=row.HexByte(2); Interval=row.HexByte(3);
        Rectangle=row.HexWord(4); Steps=row.HexByte(5); FinalCollision=(byte)row.HexByte(6); Source=row.RequiredString(7);
        Map=OracleAssetCache.ReadBytes("res://assets/oracle/objects/mermaid_wall_map.bin");
        Attributes=OracleAssetCache.ReadBytes("res://assets/oracle/objects/mermaid_wall_flg.bin");
        LayoutScratch=OracleAssetCache.ReadBytes("res://assets/oracle/objects/mermaid_wall_layout_scratch.bin");
        if (Map.Length != 576 || Attributes.Length != 576 || LayoutScratch.Length != 176 || Steps != 15 || Rectangle != 0x260c ||
            InitialWait == 0 || SecondWait == 0 || Interval == 0)
            throw new InvalidOperationException($"{Source}: unsupported wall retraction geometry/timing.");
    }
}
