
namespace Oracles;

internal sealed class AgesRickyDepartureDatabase
{
    internal RickyDepartureParameters Parameters { get; }
    internal AgesRickyDepartureDatabase()
    {
        var row=GeneratedTable.Load(AgesGameContent.AssetRoot+"cutscenes/ricky_departure.tsv",
            new("Ages rickyStateASubstate2-7",GeneratedTableKeySemantics.Ordered,
                ["air-animation","ground-animation-base","punch-animation","diagonal-angle","wall-probe-angle","exit-angle","hop-delay","left-mask","source"],headerRequired:true)).SingleRow();
        Parameters=new(row.HexByte(0),row.HexByte(1),row.HexByte(2),row.HexByte(3),row.HexByte(4),row.HexByte(5),row.UnsignedDecimal(6),row.HexByte(7));
        _=row.RequiredString(8);
        if(Parameters!=new RickyDepartureParameters(3,5,0x18,0x14,0x18,0x10,8,0x40))
            throw new InvalidOperationException("ricky.s: Ages departure animation/angle/counter/left-state contract changed.");
    }
}
