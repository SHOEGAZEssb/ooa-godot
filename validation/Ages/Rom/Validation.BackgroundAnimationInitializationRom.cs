using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBackgroundAnimationInitializationRom()
    {
        ReinitializeGameplayForValidation();LoadValidationRoom(0,0x38);_entities.Clear();
        var data=new OracleAnimationData();
        var baseline=OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + "gfx/gfx_tileset22.png");
        foreach(bool direct in new[]{false,true})
        for(int group=0;group<22;group++)
        {
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0,0);rom.EnableVramDma();
            rom.LoadCutsceneGraphics(0x70,0x30);rom.InitializeBackgroundAnimations(group,direct);
            // The credits freeze initialized graphics immediately. Direct
            // loads retain their queued first writes, which drain later.
            for(int update=0;update<=(direct?20:group==18?35:0);update++)
            {
                if(update!=0)rom.AdvanceBackgroundAnimations();
                if(!direct&&update!=0&&update!=35)continue;
                int[] headers=direct?data.GetActiveHeadersAfterDirectLoad(group,update):data.GetActiveHeaders(group,update);
                for(int tile=0;tile<256;tile++)
                {
                    Image image=baseline;int sourceTile=tile;
                    if(data.TryGetOverride(headers,tile,out var animated,out int animatedTile)){image=animated;sourceTile=animatedTile;}
                    int columns=image.GetWidth()/8;
                    for(int y=0;y<8;y++)for(int x=0;x<8;x++)
                    {
                        int shade=Mathf.RoundToInt((1-image.GetPixel(sourceTile%columns*8+x,sourceTile/columns*8+y).R)*3);
                        int at=0x8800+tile*16+y*2;
                        int native=((rom.VramByte(1,at)>>(7-x))&1)|(((rom.VramByte(1,at+1)>>(7-x))&1)<<1);
                        FailIf(shade!=native,$"Animation group${group:x2} direct={direct} update{update} tile${tile:x2} pixel{x},{y}: port{shade}/ROM{native}.");
                    }
                }
            }
        }
    }
}
