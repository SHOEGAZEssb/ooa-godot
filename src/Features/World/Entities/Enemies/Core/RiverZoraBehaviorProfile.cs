namespace Oracles;

internal readonly record struct RiverZoraBehaviorProfile(
    int SpawnXLimit, int SpawnYMask, int SurfacingFrames,
    int HiddenCounterMask, int HiddenCounterBase, int WaterTileBase,
    int WaterTileCount)
{
    internal static RiverZoraBehaviorProfile For(string root)
    {
        var row=GeneratedTable.Load(root+"metadata/river_zora_behavior.tsv",
            new("River Zora native behavior",GeneratedTableKeySemantics.Unique,
                ["spawn-x-limit","spawn-y-mask","surfacing-frames","hidden-mask","hidden-base","water-base","water-count","source"],
                ["source"],headerRequired:true)).SingleRow();
        _=row.RequiredString(7);
        return new(row.UnsignedDecimal(0),row.UnsignedDecimal(1),row.UnsignedDecimal(2),row.UnsignedDecimal(3),
            row.UnsignedDecimal(4),row.UnsignedDecimal(5),row.UnsignedDecimal(6));
    }
}
