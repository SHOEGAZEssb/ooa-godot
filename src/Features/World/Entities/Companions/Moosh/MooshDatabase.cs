namespace Oracles;

internal sealed class MooshDatabase
{
    internal CompanionVisualDatabase Visual { get; }
    internal int WaterHazard { get; }
    internal int WaterHoverFrames { get; }
    internal int WaterExclamationZOffset { get; }

    internal MooshDatabase(string assetRoot)
    {
        Visual=new(assetRoot,SpecialObjectId.Moosh);
        var row=GeneratedTable.Load(assetRoot+"metadata/moosh_hover.tsv",
            new("mooshState8Substate1",GeneratedTableKeySemantics.Ordered,
                ["water-hazard","frames","exclamation-z-offset","source"],headerRequired:true)).SingleRow();
        WaterHazard=row.UnsignedDecimal(0);WaterHoverFrames=row.UnsignedDecimal(1);WaterExclamationZOffset=row.Decimal(2,-128,127);
        _=row.RequiredString(3);
    }
}
