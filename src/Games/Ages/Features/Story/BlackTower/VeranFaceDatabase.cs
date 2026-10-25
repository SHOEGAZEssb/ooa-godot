using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class VeranFaceDatabase
{
    private const string Root=AgesGameContent.AssetRoot + "cutscenes/veran_face_";
    internal IReadOnlyList<CutsceneCommand> Commands {get;}=CutsceneCommandCatalog.Load(Root+"commands.tsv");
    internal byte[] Warp {get;}
    internal IReadOnlyDictionary<int,Color[]> Palettes {get;}
    internal VeranFaceDatabase()
    {
        var row=GeneratedTable.Load(Root+"tables.tsv",new("Veran face warp",GeneratedTableKeySemantics.Unique,
            ["profile","values","source"],["profile"],headerRequired:true)).SingleRow();
        Warp=Array.ConvertAll(row.SplitRequired(1,','),v=>Convert.ToByte(v,16));
        if(row.RequiredString(0)!="warp"||Warp.Length!=5||Warp[4]!=3)
            throw new InvalidOperationException("veranCutsceneFace.s:@warpDestVariables requires five warp bytes and CUTSCENE_03.");
        var colors=OracleGraphicsData.LoadPalette(Root+"palettes.bin",2,6);
        Palettes=new Dictionary<int,Color[]> {[6]=[colors[6,0],colors[6,1],colors[6,2],colors[6,3]],
            [7]=[colors[7,0],colors[7,1],colors[7,2],colors[7,3]]};
    }
}
