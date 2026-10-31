using Godot;

namespace Oracles;

internal sealed class DimitriVisualDatabase
{
    internal string AssetRoot { get; }
    internal string[] Animations { get; }
    internal int[][] SourceOffsets { get; }
    internal Texture2D[] LinkTextures { get; }
    internal Texture2D[] LinkDamageTextures { get; }
    internal Vector2[] LinkOffsets { get; }
    internal NpcRecord CreateNativePresenter(int group,int room,Vector2 position)=>new(group,room,SpecialObjectId.Dimitri,0,
        (int)position.Y,(int)position.X,0,0,"spr_dimitri",0,2,0,false,Animations[0x24],Animations[0x24],
        Animations[0x24],Animations[0x24],string.Empty,NpcImplementationClassification.EventOwned);

    internal DimitriVisualDatabase(string assetRoot)
    {
        AssetRoot=assetRoot;
        var data=new CompanionVisualDatabase(assetRoot,SpecialObjectId.Dimitri);
        Animations=data.Animations;SourceOffsets=data.SourceOffsets;
        string[] linkFrames=data.LinkFrames;int[] offsets=data.LinkSourceOffsets;
        LinkTextures=new Texture2D[linkFrames.Length];
        LinkDamageTextures=new Texture2D[linkFrames.Length];
        LinkOffsets=new Vector2[linkFrames.Length];
        Image source=OracleGraphicsCache.LoadImage(AssetRoot+"gfx/spr_link.png");
        for(int index=0;index<linkFrames.Length;index++)
        {
            string oam=OracleGraphicsCache.GetAnimationDefinition(linkFrames[index]).Frames[0].EncodedOam;
            (LinkTextures[index],LinkOffsets[index])=OracleOamCompositor.BuildPositionedOamTexture(
                source,oam,0,0,null,sourceGrayscaleInverted:true,sourceOffset:offsets[index]);
            (LinkDamageTextures[index],_)=OracleOamCompositor.BuildPositionedOamTexture(
                source,oam,0,0,OracleOamCompositor.GetStandardSpritePalette(5),sourceGrayscaleInverted:true,sourceOffset:offsets[index]);
        }
    }
}
