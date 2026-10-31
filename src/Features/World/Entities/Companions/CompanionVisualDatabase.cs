using Godot;
using System;
using System.Linq;

namespace Oracles;

internal sealed class CompanionVisualDatabase
{
    internal string AssetRoot { get; }
    internal string Sprite { get; }
    internal int TileBase => 0;
    internal int Palette { get; }
    internal string[] Animations { get; }
    internal int[][] SourceOffsets { get; }
    internal string LinkSprite => "spr_link";
    internal int LinkPalette => 0;
    internal string[] LinkFrames { get; }
    internal int[] LinkSourceOffsets { get; }
    internal string Source { get; }
    private readonly int _id;
    internal NpcRecord CreateNativePresenter(int group,int room,Vector2 position,int animation)=>new(group,room,_id,0,
        (int)position.Y,(int)position.X,0,0,Sprite,TileBase,Palette,0,false,Animations[animation],Animations[animation],
        Animations[animation],Animations[animation],string.Empty,NpcImplementationClassification.EventOwned);

    internal CompanionVisualDatabase(string assetRoot,int id)
    {
        AssetRoot=assetRoot;_id=id;
        var (name,palette,count)=id switch
        {
            SpecialObjectId.Ricky=>("ricky",3,37),
            SpecialObjectId.Dimitri=>("dimitri",2,40),
            SpecialObjectId.Moosh=>("moosh",1,27),
            _=>throw new NotSupportedException($"specialObjectAnimationData.s: SPECIALOBJECT ${id:x2} has no shared visual binding.")
        };
        var row=GeneratedTable.Load(AssetRoot+$"metadata/{name}_visual.tsv",
            new($"SPECIALOBJECT ${id:x2} visuals",GeneratedTableKeySemantics.Ordered,
                ["sprite","palette","animations-base64","animation-source-offsets-base64",
                 "link-frames-base64","link-source-offsets","source"],headerRequired:true)).SingleRow();
        Sprite=row.RequiredString(0);Palette=row.UnsignedDecimal(1);
        if(Sprite!="spr_"+name || Palette!=palette)throw row.Invalid(0,$"native {name} sprite/palette{palette}");
        Animations=row.Base64Utf8(2).Split('\n');
        SourceOffsets=row.Base64Utf8(3).Split('\n').Select(line=>line.Split(',').Select(value=>Convert.ToInt32(value,16)).ToArray()).ToArray();
        if(Animations.Length!=count || SourceOffsets.Length!=count)throw row.Invalid(2,$"{count} native {name} animation/source-offset streams");
        for(int index=0;index<count;index++)
            if(OracleGraphicsCache.GetAnimationDefinition(Animations[index]).Frames.Length!=SourceOffsets[index].Length)
                throw row.Invalid(3,$"animation${index:x2} source offsets for every frame");
        LinkFrames=row.Base64Utf8(4).Split('\n');
        LinkSourceOffsets=row.RequiredString(5).Split(',').Select(value=>Convert.ToInt32(value,16)).ToArray();
        if(LinkFrames.Length!=0x33 || LinkSourceOffsets.Length!=0x33)throw row.Invalid(4,"native riding Link poses$00-$32 and source offsets");
        Source=row.RequiredString(6);
    }
}
