using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class CreditsHorizontalDatabase
{
    private readonly byte[] _bytes;
    private readonly Dictionary<int,(int Offset,int Direction,int Animation)> _entries=[];
    private readonly Dictionary<int,IntroOamPart[]> _frames=[];
    internal CreditsHorizontalDatabase()
    {
        byte[]? bytes=null;
        foreach(var row in GeneratedTable.Load(EndingScreenDatabase.Root+"credits_horizontal.tsv",new("INTERAC $ae horizontal credits",GeneratedTableKeySemantics.Unique,
            ["kind","index","data","source"],["kind","index"],headerRequired:true)).Rows)
        {
            int index=row.Decimal(1);
            switch(row.RequiredString(0))
            {
                case "bytes":bytes=Convert.FromHexString(row.RequiredString(2));break;
                case "entry":
                    var fields=Array.ConvertAll(row.SplitRequired(2,','),int.Parse);
                    if(fields.Length!=3)throw row.Invalid(2,"script offset, direction and animation");
                    _entries.Add(index,(fields[0],fields[1],fields[2]));break;
                case "animation":
                    var frame=OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(2)).Frames[0];
                    _frames.Add(index,frame.EncodedOam.Split(';').Select(part=>
                    {
                        var values=Array.ConvertAll(part.Split(','),int.Parse);
                        if(values.Length!=4)throw row.Invalid(2,"four-byte OAM composition");
                        return new IntroOamPart(values[0],values[1],values[2],values[3]);
                    }).ToArray());break;
                default:throw row.Invalid(0,"bytes, entry or animation");
            }
        }
        _bytes=bytes??throw new InvalidOperationException("creditsTextHorizontal.s: missing contiguous script bytes.");
        if(_entries.Count!=8||_frames.Count!=27)throw new InvalidOperationException("INTERAC $ae: incomplete normal/linked script and animation tables.");
    }
    internal (int Offset,int Direction,int Animation) Entry(int index)=>_entries[index];
    internal byte Read(int offset)=>(uint)offset<_bytes.Length?_bytes[offset]:throw new NotSupportedException($"creditsTextHorizontal.s: script read beyond imported stream at ${offset:x4}.");
    internal IntroOamPart[] Frame(int animation)=>_frames.TryGetValue(animation,out var parts)?parts:throw new NotSupportedException($"INTERAC $ae animation ${animation:x2} is not represented.");
}
