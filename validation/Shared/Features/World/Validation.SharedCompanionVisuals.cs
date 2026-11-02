
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedCompanionVisuals(FrontendValidationRoot root)
    {
        CheckSharedCompanionVisual(root,0x0b);
        CheckSharedCompanionVisual(root,0x0c);
        CheckSharedCompanionVisual(root,0x0d);
    }

    private static void CheckSharedCompanionVisual(FrontendValidationRoot root,int id)
    {
        var rooms=root._gameplaySession!.Rooms;
        var game=rooms.RuntimeState.Game;
        var data=new CompanionVisualDatabase(rooms.AssetRoot,id);
        bool dimitri=id==0x0c;int count=id==0x0b?37:dimitri?40:27,palette=id==0x0b?3:dimitri?2:1;
        if(id==0x0b)
        {
            var bodyData=new RickyDatabase(rooms.AssetRoot);var behavior=bodyData.Behavior;
            FailIf(bodyData.IdleHopSpeedZ!=-256 || bodyData.IdleGravity!=0x40 ||
                behavior.VineTopTile!=(game==OracleGame.Ages?0xd4:0xdd) || behavior.GroundSpeed!=0x1e || behavior.HopSpeedZ!=-384 ||
                behavior.PunchDamage!=4 || behavior.TornadoDamage!=4 || behavior.PunchBoxes.Length!=4 ||
                behavior.PunchBoxes[1]!=new RickyPunchBox(12,18,-2,8) || behavior.TornadoRadiusX!=6 || behavior.TornadoRadiusY!=6,
                $"{game}: shared Ricky data must retain native hop/attack parameters and the selected build's vine tile.");
        }
        if(id==0x0d)
        {
            var bodyData=new MooshDatabase(rooms.AssetRoot);
            FailIf(bodyData.WaterHazard!=1 || bodyData.WaterHoverFrames!=60 || bodyData.WaterExclamationZOffset!=-32,
                $"{game}: native mooshState8Substate1 must retain hazard$01, counter$3c and exclamation Z-$20.");
        }
        var biting=OracleGraphicsCache.GetAnimationDefinition(data.Animations[0x14]);
        // Native three-frame entry loops to frame1 after a full replacement upload.
        FailIf(dimitri && (biting.Frames.Length!=4 || biting.LoopStart!=2),
            $"{game}: Dimitri animation$14 must retain its initial partial-upload pose before the stable two-frame loop.");
        var rom=ValidationRom.LoadCleanUs(game);
        var aliases=CompanionNativeAnimationDatabase.For(rooms.AssetRoot);
        var memory=OracleRuntimeState.CreateInitialized(game);
        memory.SetWramByte(0xd101,(byte)id);memory.SetWramByte(0xd11a,0x80);
        var foreignMemory=OracleRuntimeState.CreateInitialized(game==OracleGame.Ages?OracleGame.Seasons:OracleGame.Ages);
        foreignMemory.SetWramByte(0xd101,(byte)id);foreignMemory.SetWramByte(0xd11a,0x80);foreignMemory.SetWramByte(0xd132,0xff);
        foreach(Action invalid in new Action[]{
            ()=>aliases.PublishAnimation(memory,id,0xff,0,1,0),
            ()=>aliases.PublishAnimation(memory,id==0x0b?0x0c:0x0b,0,0,1,0),
            ()=>aliases.PublishGraphics(foreignMemory)})
        {
            byte[] before=memory.CaptureState().Wram,foreignBefore=foreignMemory.CaptureState().Wram;bool rejected=false;
            try{invalid();}catch(ArgumentException){rejected=true;}catch(NotSupportedException){rejected=true;}
            FailIf(!rejected || !memory.CaptureState().Wram.AsSpan().SequenceEqual(before) ||
                !foreignMemory.CaptureState().Wram.AsSpan().SequenceEqual(foreignBefore),
                "Invalid native animation or a foreign state owner must fail before changing bytes.");
        }
        // Literal clean-US table addresses, independently of importer symbols/macros.
        int graphics=id==0x0b?(game==OracleGame.Ages?0x1a4eb:0x1a1bc):dimitri?(game==OracleGame.Ages?0x1a7c8:0x1a499):(game==OracleGame.Ages?0x1aa5e:0x1a72f);
        int oamTable=id==0x0b?(game==OracleGame.Ages?0x1a760:0x1a431):dimitri?(game==OracleGame.Ages?0x1a9fc:0x1a6cd):(game==OracleGame.Ages?0x1ac6e:0x1a94b);
        int linkGraphics=game==OracleGame.Ages?0x1a201:0x19ed2;
        int linkOam=game==OracleGame.Ages?0x1a0a7:0x19d9e;
        int oamBank=game==OracleGame.Ages?0x13:0x12;
        int Word(int at)=>rom.Span[at]|rom.Span[at+1]<<8;
        int Oam(int table,int index)=>oamBank*0x4000+(Word(table+index*2)&0x3fff);
        void Upload(byte[] vram,int word)
        {
            if(word==0) return;
            int source=(0x1a+(word&1))*0x4000+(word&0x3fe0);
            rom.Span.Slice(source,(word&0x1e)*16).CopyTo(vram);
        }
        var node=new Node2D();var restoredNode=new Node2D();
        try
        {
            var animation=new EnemyAnimationPlayer(node,count);
            animation.Load(OracleGraphicsCache.LoadImage(rooms.AssetRoot+$"gfx/{data.Sprite}.png"),
                data.Animations,0,palette,positionedOam:true,animationSourceOffsets:data.SourceOffsets);
            var restored=new EnemyAnimationPlayer(restoredNode,count);
            restored.Load(OracleGraphicsCache.LoadImage(rooms.AssetRoot+$"gfx/{data.Sprite}.png"),
                data.Animations,0,palette,positionedOam:true,animationSourceOffsets:data.SourceOffsets);
            var native=new LinkCollisionRom(game:game);
            native[0xd100]=1; native[0xd101]=(byte)id;
            for(int mode=0;mode<count;mode++)
            {
                animation.SetAnimation(mode);
                memory.SetWramByte(0xd132,0xff);
                native.Call(game==OracleGame.Ages?0x2b0a:0x2a51,objectPage:0xd1,accumulator:mode);
                var vram=new byte[0x1000]; int previous=-1;
                int frames=OracleGraphicsCache.GetAnimationDefinition(data.Animations[mode]).Frames.Length;
                for(int frame=0;frame<frames+2;frame++)
                {
                    FailIf(animation.CurrentParameter!=native[0xd121] || animation.RemainingUpdates!=native[0xd120],
                        $"{game}: native SPECIALOBJECT ${id:x2} animation${mode:x2} frame{frame} counter/parameter differs.");
                    int gfx=native[0xd131];
                    aliases.PublishAnimation(memory,id,mode,animation.FrameIndex,animation.RemainingUpdates,animation.CurrentParameter);
                    foreach(int at in new[]{0xd120,0xd121,0xd122,0xd123,0xd130,0xd131})
                        FailIf(memory.ReadWramByte(at)!=native[at],
                            $"{game}: SPECIALOBJECT ${id:x2} animation${mode:x2}/frame{frame} native animation alias${at:x4} differs.");
                    aliases.PublishGraphics(memory);
                    int expectedOam=Word(oamTable+rom.Span[graphics+gfx*3]*2)&0x3fff;
                    FailIf(memory.ReadWramByte(0xd132)!=gfx ||
                        (memory.ReadWramByte(0xd11e)|memory.ReadWramByte(0xd11f)<<8)!=expectedOam,
                        $"{game}: SPECIALOBJECT ${id:x2} graphics${gfx:x2} must publish the native OAM pointer and cache byte.");
                    memory.SetWramByte(0xd11e,0xa5);aliases.PublishGraphics(memory);
                    FailIf(memory.ReadWramByte(0xd11e)!=0xa5,"Unchanged native graphics must retain the previous OAM address bytes.");
                    memory.SetWramByte(0xd11e,(byte)expectedOam);
                    if(gfx!=previous) { Upload(vram,Word(graphics+gfx*3+1)); previous=gfx; }
                    int oam=Oam(oamTable,rom.Span[graphics+gfx*3]);
                    CheckNativeCompanionTexture(rom,vram,oam,animation.CurrentTexture,animation.CurrentOffset,palette,false,
                        $"{game} SPECIALOBJECT ${id:x2} animation${mode:x2} frame{frame}");
                    // Dimitri's partial-upload entries revisit these cursors with different retained VRAM.
                    bool ambiguous=dimitri && (mode==0x14 && gfx==0x32 || mode==0x27 && gfx==8);
                    bool terminal=id==0x0d && (native[0xd121]&0x80)!=0 || id==0x0b && native[0xd121]==0xff;
                    FailIf(aliases.CanRestorePresentation(memory)==ambiguous,
                        $"{game}: companion ${id:x2} animation${mode:x2} must reject exactly the ambiguous retained-upload poses.");
                    int[] fields=[0xd120,0xd121,0xd122,0xd123,0xd130,0xd131];
                    byte[] original=Array.ConvertAll(fields,at=>native[at]);
                    foreach(int remaining in new[]{0,1,Math.Max(1,native[0xd120]-1)})
                    {
                        native[0xd120]=(byte)remaining;native[0xd121]=0;
                        foreach(int at in fields)memory.SetWramByte(at,native[at]);
                        bool resolved=aliases.TryRestoreAnimation(memory,restored);
                        FailIf(resolved==ambiguous,$"{game}: companion ${id:x2} native cursor reconstruction admission differs.");
                        if(resolved)
                        {
                            FailIf(restored.FrameIndex!=animation.FrameIndex || restored.RemainingUpdates!=remaining || restored.CurrentParameter!=0 ||
                                restored.CurrentOffset!=animation.CurrentOffset ||
                                OracleGraphicsCache.PixelHash(restored.CurrentTexture.GetImage())!=OracleGraphicsCache.PixelHash(animation.CurrentTexture.GetImage()),
                                $"{game}: companion ${id:x2} reconstruction lost its native frame/counter/consumed parameter or retained graphics.");
                            // Native handlers consume terminal signals before the bytecode can expire.
                            if(!terminal || remaining!=1)
                            {
                                restored.Advance();native.Call(game==OracleGame.Ages?0x2aef:0x2a36,objectPage:0xd1);
                                aliases.PublishAnimation(memory,id,mode,restored.FrameIndex,restored.RemainingUpdates,restored.CurrentParameter);
                                foreach(int at in fields)FailIf(memory.ReadWramByte(at)!=native[at],
                                    $"{game}: companion ${id:x2} restored animation${mode:x2} counter${remaining:x2} diverged at native byte${at:x4}.");
                            }
                        }
                        for(int field=0;field<fields.Length;field++)
                        {native[fields[field]]=original[field];memory.SetWramByte(fields[field],original[field]);}
                    }
                    if(terminal)
                    {
                        FailIf(frame!=frames-1 || native[0xd120]!=0x7f,
                            $"{game}: companion ${id:x2} must consume its final $7f-duration handler signal before another animation visit.");
                        break;
                    }
                    int ticks=native[0xd120];
                    for(int tick=0;tick<ticks;tick++)
                    {
                        animation.Advance();
                        native.Call(game==OracleGame.Ages?0x2aef:0x2a36,objectPage:0xd1);
                    }
                }
            }
        }
        finally { node.Free();restoredNode.Free(); }
        var (linkTextures,_,linkDamageTextures,linkOffsets)=CompanionLinkFrames.Load(rooms.AssetRoot,data.LinkSprite,data.LinkPalette,data.LinkFrames,data.LinkSourceOffsets);
        for(int parameter=0;parameter<0x33;parameter++)
        {
            var vram=new byte[0x1000];
            int upload=parameter==0x32?0x2f:parameter;
            Upload(vram,Word(linkGraphics+upload*3+1));
            int oam=Oam(linkOam,rom.Span[linkGraphics+parameter*3]);
            CheckNativeCompanionTexture(rom,vram,oam,linkTextures[parameter],linkOffsets[parameter],0,false,
                $"{game} riding Link pose${parameter:x2}");
            CheckNativeCompanionTexture(rom,vram,oam,linkDamageTextures[parameter],linkOffsets[parameter],5,true,
                $"{game} hurt riding Link pose${parameter:x2}");
        }
    }

    private static void CheckNativeCompanionTexture(ReadOnlyMemory<byte> rom,byte[] vram,int oam,
        Texture2D texture,Vector2 offset,int palette,bool forcedPalette,string context)
    {
        int count=rom.Span[oam];
        FailIf(count is <1 or >32,context+": invalid native OAM extent.");
        int X(int cell)=>unchecked((sbyte)rom.Span[oam+2+cell*4])+8;
        int Y(int cell)=>unchecked((sbyte)rom.Span[oam+1+cell*4]);
        int left=int.MaxValue,top=int.MaxValue,right=int.MinValue,bottom=int.MinValue;
        for(int cell=0;cell<count;cell++)
        {
            left=Math.Min(left,X(cell)); top=Math.Min(top,Y(cell));
            right=Math.Max(right,X(cell)+8); bottom=Math.Max(bottom,Y(cell)+16);
        }
        int width=right-left,height=bottom-top;
        using Image image=texture.GetImage();
        FailIf(offset!=new Vector2(left-16,top-16) || image.GetWidth()!=width || image.GetHeight()!=height,
            context+": native OAM origin/extent differs.");
        var pixels=new int[width*height]; Array.Fill(pixels,-1);
        for(int cell=count-1;cell>=0;cell--)
        {
            int tile=rom.Span[oam+3+cell*4]&0xfe,flags=rom.Span[oam+4+cell*4];
            int nativePalette=forcedPalette?palette:palette^(flags&7);
            for(int y=0;y<16;y++)
            for(int x=0;x<8;x++)
            {
                int sx=(flags&0x20)!=0?7-x:x,sy=(flags&0x40)!=0?15-y:y;
                int at=tile*16+sy*2;
                int shade=(vram[at]>>(7-sx)&1)|((vram[at+1]>>(7-sx)&1)<<1);
                if(shade==0) continue;
                // Native standard OBJ palettes0/1/2/5, expressed as RGB555.
                int rgb=(nativePalette,shade) switch
                {
                    (0 or 1 or 2 or 3,1)=>0, (0,2)=>0x22a2, (1,2)=>0x7e03, (2,2)=>0x143f, (3,2)=>0x05ff,
                    (0 or 1 or 2 or 3,3)=>0x475f, (5,1)=>0x1adf, (5,2)=>0x001b, (5,3)=>0,
                    _=>throw new InvalidOperationException(context+$": untraced native palette${nativePalette:x2}.")
                };
                pixels[(Y(cell)-top+y)*width+X(cell)-left+x]=rgb;
            }
        }
        for(int y=0;y<height;y++)
        for(int x=0;x<width;x++)
        {
            Color color=image.GetPixel(x,y);
            int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
            int expected=pixels[y*width+x];
            FailIf(expected<0?color.A!=0:color.A!=1 || rgb!=expected,
                context+$": native OBJ pixel{x},{y} differs (RGB${rgb:x4}, expected${expected:x4}).");
        }
    }
}
