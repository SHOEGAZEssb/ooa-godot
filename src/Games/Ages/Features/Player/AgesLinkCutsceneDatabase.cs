using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class AgesLinkCutsceneDatabase
{
    private readonly NewGameIntroDatabase _frames=new(AgesGameContent.AssetRoot);
    private readonly Dictionary<int,int> _loops=[];
    internal static AgesLinkCutsceneDatabase Shared {get;}=new();
    private AgesLinkCutsceneDatabase()
    {
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/link_cutscene_loops.tsv",new("Ages cutscene Link animation loops",GeneratedTableKeySemantics.Unique,
            ["index","loop-start","source"],["index"],headerRequired:true)).Rows)
        {
            int index=row.HexByte(0),loop=row.Decimal(1);
            if(loop>=Frames(index).Length)throw row.Invalid(1,"native animation loop frame");
            _loops.Add(index,loop);
        }
        if(_loops.Count!=13)throw new InvalidOperationException("specialObject08AnimationDataPointers: incomplete bound Ages cutscene Link animations.");
    }
    internal ObjectSpriteFrame[] Frames(int animation)=>_frames.SpriteFrames($"link-cutscene-{animation:x2}");
    internal int Loop(int animation)=>_loops[animation];
}
