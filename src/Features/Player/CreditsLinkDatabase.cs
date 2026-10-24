using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CreditsLinkDatabase
{
    private readonly NewGameIntroDatabase _frames=new();
    private readonly Dictionary<int,int> _loops=[];
    internal static CreditsLinkDatabase Shared {get;}=new();
    private CreditsLinkDatabase()
    {
        foreach(var row in GeneratedTable.Load("res://assets/oracle/cutscenes/credits_link_loops.tsv",new("Credits Link animation loops",GeneratedTableKeySemantics.Unique,
            ["index","loop-start","source"],["index"],headerRequired:true)).Rows)
        {
            int index=row.HexByte(0),loop=row.Decimal(1);
            if(loop>=Frames(index).Length)throw row.Invalid(1,"native animation loop frame");
            _loops.Add(index,loop);
        }
        if(_loops.Count!=11)throw new InvalidOperationException("specialObject08AnimationDataPointers: incomplete credits Link animations.");
    }
    internal IntroSpriteFrame[] Frames(int animation)=>_frames.SpriteFrames($"link-credits-{animation:x2}");
    internal int Loop(int animation)=>_loops[animation];
}
