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
    internal byte? FinalCollision { get; }
    internal int Room { get; }
    internal byte[] Map { get; }
    internal byte[] Attributes { get; }
    internal byte[] LayoutScratch { get; }
    internal string Source { get; }
    internal WallRetractionDatabase(int dungeon = 0x0c)
    {
        if (dungeon is not (6 or 8 or 0x0c))
            throw new NotSupportedException($"miscCutscenes.s: unsupported wall retraction dungeon${dungeon:x2}.");
        bool ancient = dungeon == 8;
        string prefix = ancient ? "ancient_tomb" : "mermaid";
        Room = ancient ? 0x95 : 0x43;
        var row=GeneratedTable.Load($"res://assets/oracle/objects/{prefix}_wall_retraction.tsv",
            new GeneratedTableSchema("Native wall retraction",GeneratedTableKeySemantics.Ordered,
                ["initial-wait","second-wait","shake","interval","rectangle","steps","final-collision","source"],headerRequired:true)).SingleRow();
        InitialWait=row.HexByte(0); SecondWait=row.HexByte(1); Shake=row.HexByte(2); Interval=row.HexByte(3);
        Rectangle=row.HexWord(4); Steps=row.HexByte(5); FinalCollision=row.RequiredString(6) == "-" ? null : (byte)row.HexByte(6); Source=row.RequiredString(7);
        Map=OracleAssetCache.ReadBytes($"res://assets/oracle/objects/{prefix}_wall_map.bin");
        Attributes=OracleAssetCache.ReadBytes($"res://assets/oracle/objects/{prefix}_wall_flg.bin");
        LayoutScratch=OracleAssetCache.ReadBytes($"res://assets/oracle/objects/{prefix}_wall_layout_scratch.bin");
        if (Map.Length != (ancient ? 384 : 576) || Attributes.Length != Map.Length || LayoutScratch.Length != 176 ||
            Steps != (ancient ? 11 : 15) || Rectangle != (ancient ? 0x4d04 : 0x260c) ||
            ancient && FinalCollision is not null ||
            InitialWait == 0 || SecondWait == 0 || Interval == 0)
            throw new InvalidOperationException($"{Source}: unsupported wall retraction geometry/timing.");
    }
}
