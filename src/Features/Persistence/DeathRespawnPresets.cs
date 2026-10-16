using System;
using System.Linq;

namespace oracleofages;

internal static class DeathRespawnPresets
{
    private static readonly Lazy<(byte Mask,byte[] Values)[]> Presets=new(()=>
    {
        var rows=GeneratedTable.Load("res://assets/oracle/metadata/death_respawn_presets.tsv",new("Death respawn presets",GeneratedTableKeySemantics.Ordered,
            ["index","mask","values","source"],headerRequired:true)).Rows;
        int index=0;
        var presets=rows.Select(row=>
        {
            if(row.Decimal(0)!=index++)throw row.Invalid(0,"original contiguous preset order");
            byte mask=(byte)row.HexByte(1);var values=Array.ConvertAll(row.SplitRequired(2,','),value=>Convert.ToByte(value,16));
            if(values.Length!=7||(mask&1)!=0)throw row.Invalid(2,"seven values and a terminating partial-write mask");
            return(mask,values);
        }).ToArray();
        if(presets.Length!=4)throw new InvalidOperationException("bank1.s:loadDeathRespawnBufferPreset requires the four Ages presets.");
        return presets;
    });
    internal static void Load(OracleSaveData save,int index)
    {
        var presets=Presets.Value;
        if((uint)index>=presets.Length)throw new NotSupportedException($"loadDeathRespawnBufferPreset: unsupported preset ${index:x2}.");
        save.ApplyDeathRespawnPreset(presets[index].Mask,presets[index].Values);
    }
}
