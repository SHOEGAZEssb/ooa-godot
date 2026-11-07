namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsGnarledEntranceWarp()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Save.SetRoomFlag(0, 0x96, 0x80);
                session.DebugTravel(0, 0x96);
                session.Link.WarpTo(new(72, 88));
                FailIf(session.Collision.Collides(session.Link.Position),
                    "Gnarled entrance fixture must start on actual floor south of the opened doorway.");
                app.Step(2, Vector2.Zero, batched: batched);
                for (int visit = 0; visit < 2; visit++)
                {
                    session.Memory.SetField("wToggleBlocksState",0xff);
                    session.Memory.SetField("wSwitchState",0xff);
                    session.Memory.SetField("wSpinnerState",0xff);
                    // warpSources.s:group0WarpSources selects group4 destination $02:
                    // room $1c, position $ff, parameter $09, walk arrival $03.
                    int updates = 0;
                    while (!session.Transitions.IsTransitioning && updates++ < 120)
                        app.Step(1, Vector2.Up, batched: batched);
                    FailIf(!session.Transitions.IsTransitioning,
                        "Opened Gnarled doorway must request its native warp from collision-valid overworld floor.");
                    app.Step(160, Vector2.Zero, batched: batched);
                    FailIf(session.Rooms.ActiveGroup != 4 || session.RoomId != 0x1c || session.Scrolling ||
                        session.Rooms.CurrentDungeonIndex != 1 || session.Room.Width != 240 || session.Room.Height != 176 ||
                        session.Link.Position != new Vector2I(120, 148),
                        $"Gnarled visit {visit}/batch{batched}: native $4:1c walk arrival differs: group{session.Rooms.ActiveGroup}, room${session.RoomId:x2}, XY{session.Link.Position}.");
                    FailIf(!session.Dialogue.IsOpen || session.Dialogue.SourceTextId!=0x0201 ||
                        !session.Dialogue.CurrentMessage.Contains("Gnarled Root",StringComparison.Ordinal),
                        "Walking into $4:1c must show native TX_0201 through INTERAC$12:$00.");
                    FailIf(session.Memory.ReadField("wToggleBlocksState")!=0 || session.Memory.ReadField("wSwitchState")!=0 ||
                        session.Memory.ReadField("wSpinnerState")!=0 || session.Save.RespawnGroup!=4 || session.Save.RespawnRoom!=0x1c,
                        "Dungeon $01 entry must reset the shared dungeon bytes and record its death checkpoint.");
                    // staticDungeonObjects.s:dungeon1StaticObjects retains the room $15 minecart.
                    FailIf(!Enumerable.Range(0,8).Select(index=>session.Memory.ReadWramByte(0xcd80+index))
                        .SequenceEqual(new byte[]{3,0x15,0x16,0,0x28,0x78,0,0}),
                        "Dungeon $01 must initialize its source minecart in the authoritative static-object buffer.");
                    // rooms/seasons/large/room051c.bin and statueEyeball.s scan $ae down through $01.
                    var eyes=session.Entities.Entities<StatueEyeball>();
                    FailIf(!eyes.Select(eye=>session.Room.GetPackedPosition(eye.Position))
                        .SequenceEqual(new[]{0x5c,0x59,0x55,0x52,0x3c,0x39,0x35,0x32}) ||
                        eyes.Any(eye=>!eye.Initialized || !eye.Visible || eye.AnimationIndex!=4),
                        "Gnarled entrance must allocate its eight source statue eyes in descending layout order.");
                    session.Dialogue.Close();
                    app.Step(4, Vector2.Zero, batched: batched);
                    FailIf(session.Transitions.IsTransitioning,
                        "Gnarled arrival must remain stable before Link walks back out.");
                    // group4WarpSources uses bottom-left quadrant $04 and group0 destination $01.
                    updates = 0;
                    while (!session.Transitions.IsTransitioning && updates++ < 90)
                        app.Step(1, Vector2.Down, batched: batched);
                    FailIf(!session.Transitions.IsTransitioning,
                        "Gnarled entrance must retain its native southern return warp.");
                    app.Step(160, Vector2.Zero, batched: batched);
                    FailIf(session.Rooms.ActiveGroup != 0 || session.RoomId != 0x96 || session.Scrolling ||
                        session.Link.Position != new Vector2I(72, 72) || !session.Save.HasRoomFlag(0, 0x96, 0x80),
                        $"Gnarled visit {visit}/batch{batched}: return to opened $0:96 differs: XY{session.Link.Position}.");
                    app.Step(8, Vector2.Down, batched: batched);
                }
            }
            finally { root.Free(); }
        }
    }

    private void ValidateSeasonsGnarledKeyholeRom()
    {
        foreach(var fixture in new[]{(Batched:false,Full:false),(Batched:true,Full:false),(Batched:false,Full:true)})
        {
            bool batched=fixture.Batched;
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!; var entities=session.Entities;
                var memory=session.Memory; var player=session.Link;
                session.Inventory.EquipA(0); session.Inventory.EquipB(0);
                session.Inventory.GiveTreasure(0x42,1);
                session.Save.SetRoomFlag(0,0x96,0x80,false);
                session.DebugTravel(0,0x96); player.WarpTo(new(104.25f,72.5f)); player.Face(Vector2I.Up);
                var app=new ApplicationValidationFixture(root);
                app.Step(1,Vector2.Zero,batched:batched);
                var controller=entities.EntityAdapters<SeasonsGnarledKeyholeRoomEntity>().Single();
                int address=0xd040+entities.NativeInteractionSlot(controller)*0x100;
                FailIf(!controller.CanTrigger || memory.ReadField("wDisableWarpTiles")!=1 || !entities.NativeWarpTilesDisabled ||
                    session.Room.IsSolid(player.Position) || session.Room.GetMetatile(new(104,40))!=0xec,
                    "Seasons INTERAC$21 must initialize on native floor below keyhole$26, disabling the hidden warp.");
                byte[] positions=[0x23,0x24,0x25,0x33,0x34,0x35,0x43,0x44,0x45];
                byte[] closed=[0,0,0,0,0,0,4,12,8],opened=[1,3,2,15,15,15,15,12,15];
                int Collision(byte position)=>session.Room.GetTerrainInfo(new((position&15)*16+8,(position>>4)*16+8)).Collision;
                FailIf(!positions.Select(Collision).SequenceEqual(closed.Select(value=>(int)value)) || session.Room.UniqueGraphicsOverride!=0x20,
                    "Seasons INTERAC$21 must install all nine native closed collisions and graphics$20.");
                var native=new BraceletRom(session.Room,player.PrecisePosition,0,session.Inventory.ActiveRing,game:OracleGame.Seasons);
                native.LoadSave(session.Save); native.InitializeWalkingAnimation();
                native.SetNativeByte(address,1);native.SetNativeByte(address+1,0x21);
                native.SetNativeByte(address+0x0b,0x28);native.SetNativeByte(address+0x0d,0x68);
                native[0xcc2d]=0;native[0xcc2e]=0;
                foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wInformativeTextsShown","wScrollMode"})
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                native.SetNativeByte(0xcc51,(byte)session.Sound.NativeActiveMusic);
                native.SetNativeByte(0xcc62,(byte)session.Sound.NativeRoomMusic);
                // Native state0 installs its own script pointer; it consumes no RNG or cue.
                native.AdvanceInteractions(entities.FrameCounter);
                native.LoadUniqueGraphics(0x20);
                byte[] graphicsBytes=Enumerable.Range(0,512).Select(native.UniqueGraphicsByte).ToArray();
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=session.Sound.AttachPlayRequestAudit();
                var spriteRom=new LinkCollisionRom(game:OracleGame.Seasons);
                spriteRom.Call(0x0602,bank:0,accumulator:0x83); // GFXH_COMMON_SPRITES -> bank1:$8000.
                spriteRom.Call(0x04e7,bank:0,accumulator:0x0f); // PALH_0f supplies OBJ0-5.
                var spriteFrames=new System.Collections.Generic.HashSet<(int Subid,int Oam)>();
                int updates=0,completedAt=0; var stages=new List<(int Update,int Header)>();
                void Step(int count,Vector2 movement=default)=>app.Step(count,movement,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,movement==Vector2.Up?0x40:0,interactions:true);
                    native.AdvanceScreenShake();
                    string context=$"Seasons Gnarled batch{batched}/update{++updates}";
                    foreach(string field in new[]{"wDisabledObjects","wMenuDisabled","wDisableWarpTiles","wLinkForceState","wcc50","wTmpcfc0","wPushingAgainstTileCounter"})
                        FailIf(memory.ReadField(field)!=native.ReadNativeByte(memory.FieldAddress(field)),context+$": {field} differs, port=${memory.ReadField(field):x2}, ROM=${native.ReadNativeByte(memory.FieldAddress(field)):x2}.");
                    FailIf(session.Sound.NativeActiveMusic!=native.ReadNativeByte(0xcc51) || session.Sound.NativeRoomMusic!=native.ReadNativeByte(0xcc62),
                        $"{context}: native active/room music differs: port=${session.Sound.NativeActiveMusic:x2}/${session.Sound.NativeRoomMusic:x2}, ROM=${native.ReadNativeByte(0xcc51):x2}/${native.ReadNativeByte(0xcc62):x2}.");
                    FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f),context+": native Link position differs.");
                    FailIf(session.Save.GetRoomFlags(0,0x96)!=native[0xc796],context+": native flag$80 differs.");
                    for(int i=0;i<positions.Length;i++)FailIf(Collision(positions[i])!=native[0xce00+positions[i]],context+": native collision differs.");
                    FailIf(!controller.Finished && (memory.ReadWramByte(address+4)!=native.ReadNativeByte(address+4) ||
                        memory.ReadWramByte(address+6)!=native.ReadNativeByte(address+6) || memory.ReadWramByte(address+7)!=native.ReadNativeByte(address+7)),
                        context+$": INTERAC$21 state/counters differ: port={memory.ReadWramByte(address+4)}/{memory.ReadWramByte(address+6)}/{memory.ReadWramByte(address+7)}, ROM={native.ReadNativeByte(address+4)}/{native.ReadNativeByte(address+6)}/{native.ReadNativeByte(address+7)}.");
                    var stones=entities.EntityAdapters<SeasonsGnarledStoneRoomEntity>().ToArray();
                    int nativeStones=Enumerable.Range(0xd2,14).Count(page=>native.ReadNativeByte((page<<8)|0x40)!=0 && native.ReadNativeByte((page<<8)|0x41)==0x4b);
                    FailIf(stones.Length!=nativeStones,$"{context}: native stone/dust lifetime differs.");
                    foreach(var stone in stones)
                    {
                        int at=0xd040+entities.NativeInteractionSlot(stone)*0x100;
                        foreach(int offset in new[]{0x40,0x41,0x42,0x44,0x49,0x4a,0x4b,0x4c,0x4d,0x4e,0x4f,0x50,0x54,0x55,0x5a,0x70})
                            FailIf(memory.ReadWramByte(at+offset-0x40)!=native.ReadNativeByte(at+offset-0x40),$"{context}: stone slot${at>>8:x2} byte${offset:x2} differs.");
                        // A birth below its producer waits for the next native
                        // visit. Its prepared texture is not initialized OAM.
                        if(native.ReadNativeByte(at+4)==0)
                        {
                            FailIf(stone.Npc.Visible||memory.ReadWramByte(at+0x20)!=0||memory.ReadWramByte(at+0x21)!=0,
                                $"{context}: unvisited stone slot${at>>8:x2} must remain hidden with cleared native animation bytes.");
                            continue;
                        }
                        FailIf(stone.Npc.CurrentAnimationCounter!=native.ReadNativeByte(at+0x20) ||
                            stone.Npc.CurrentAnimationParameter!=native.ReadNativeByte(at+0x21),$"{context}: stone/dust animation counter/parameter differs.");
                        int pointer=native.ReadNativeByte(at+0x1e)|(native.ReadNativeByte(at+0x1f)<<8);
                        if(spriteFrames.Add((stone.Npc.Record.SubId,pointer)))
                            CompareSeasonsGnarledStonePixels(native,spriteRom,stone.Npc,at,context);
                    }
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.SequenceEqual(native.Sounds),context+$": native cues/RNG differ: port={rng.Calls-seed.Calls}, ROM={native.RandomCalls}; cues={string.Join(',',sounds.Requests)}/{string.Join(',',native.Sounds)}.");
                    int header=session.Room.UniqueGraphicsOverride!.Value;
                    if(stages.Count==0 || stages[^1].Header!=header)
                    {
                        stages.Add((updates,header));
                        // The original queues these decompressed bytes for VBlank DMA.
                        for(int i=0;i<(header is 0x20 or 4?512:448);i++)graphicsBytes[i]=native.UniqueGraphicsByte(i);
                        using Image graphics=session.Room.CaptureLiveGraphics();
                        for(int tile=0;tile<32;tile++)for(int y=0;y<8;y++)for(int x=0;x<8;x++)
                        {
                            int at=tile*16+y*2;
                            int shade=((graphicsBytes[at]>>(7-x))&1)|(((graphicsBytes[at+1]>>(7-x))&1)<<1);
                            Color pixel=graphics.GetPixel((224+tile)%16*8+x,(224+tile)/16*8+y);
                            FailIf(pixel.R8!=255-shade*85,$"{context}: native entrance graphics${header:x2} tile${tile:x2} pixel({x},{y}) differs.");
                        }
                    }
                    if(controller.Finished && completedAt==0)completedAt=updates;
                });
                Step(50,Vector2.Up);
                FailIf(!session.Save.HasRoomFlag(0,0x96,0x80) || !session.Inventory.HasTreasure(0x42) || memory.ReadField("wDisabledObjects")!=0x81,
                    "Gnarled contact must retain treasure$42, publish flag$80/signal and disable Link through the shared owner.");
                if(fixture.Full)
                {
                    while(entities.InteractionSlotAvailable)
                    {
                        var mark=(ExclamationMarkRoomEntity)entities.TryCreateInteractionEntity(new ExclamationMarkSpawn(new(8,112),Duration:255,PlaySound:false))!;
                        int at=0xd040+entities.NativeInteractionSlot(mark)*0x100;
                        native.SetNativeByte(at,1);native.SetNativeByte(at+1,0x9f);native.SetNativeByte(at+6,255);
                        native.SetNativeByte(at+0x0b,112);native.SetNativeByte(at+0x0d,8);
                    }
                }
                session.Dialogue.ShowMessage("Pause.",120);native.SetNativeByte(0xcba0,1);
                Step(3);session.Dialogue.Close();native.SetNativeByte(0xcba0,0);
                Step(340);
                FailIf(!controller.Finished || completedAt==0 || memory.ReadField("wDisableWarpTiles")!=0 ||
                    memory.ReadField("wDisabledObjects")!=0 || memory.ReadField("wMenuDisabled")!=0 ||
                    !positions.Select(Collision).SequenceEqual(opened.Select(value=>(int)value)) ||
                    !stages.Select(stage=>stage.Header).SequenceEqual(new[]{0x20,0x21,0x22,0x23,0x04}),
                    "Gnarled entrance must finish all four graphics swaps, nine collision writes and native control release.");
                FailIf(stages[2].Update-stages[1].Update!=60 || stages[3].Update-stages[2].Update!=45 ||
                    stages[4].Update-stages[3].Update!=40 || completedAt-stages[4].Update!=36,
                    "Gnarled rise must retain source 60/45/40/35 clocks and the separate final state4 visit.");
                FailIf(!fixture.Full && (spriteFrames.Count(frame=>frame.Subid==0)!=2 || spriteFrames.Count(frame=>frame.Subid==1)!=1),
                    "Gnarled sprite proof must reach both dust OAM poses and the flying stone pose.");
                int requests=sounds.Requests.Count;
                Step(24,Vector2.Up);
                FailIf(sounds.Requests.Count!=requests || entities.Entities<OverworldKeyUseEffect>().Count!=0,
                    "Repeating the completed Gnarled keyhole must not replay its key sprite or entrance cues.");
                int debugWrites=root.DebugStateWrites;
                root._Input(new InputEventKey { Pressed=true,PhysicalKeycode=Key.Key7,ShiftPressed=true });
                FailIf(root.DebugStateWrites!=debugWrites+(fixture.Full?0:1) ||
                    !root._scene.RoomDebug.Text.Contains(fixture.Full?"S7 BUSY":"S7 SAVED",StringComparison.Ordinal),
                    "Completed Gnarled entrance must allow debug saves unless the fixture retains its indefinite exclamation helpers.");
                session.DebugTravel(0,0x95);session.DebugTravel(0,0x96);app.Step(1,Vector2.Zero,batched:batched);
                // room0096 tile$23 is $a3: tilesetCollisions03[$a3]=$10 on a fresh load.
                byte[] reloaded=[0x10,3,2,15,15,15,15,12,15];
                FailIf(entities.EntityAdapters<SeasonsGnarledKeyholeRoomEntity>().Any() || session.Room.UniqueGraphicsOverride!=4 ||
                    !positions.Select(Collision).SequenceEqual(reloaded.Select(value=>(int)value)),
                    $"Re-entry must retain the opened entrance graphics/collision and delete INTERAC$21 in state0: actors={entities.EntityAdapters<SeasonsGnarledKeyholeRoomEntity>().Count()}, header={session.Room.UniqueGraphicsOverride}, collision={string.Join(',',positions.Select(Collision))}.");
                foreach(int cancelAfter in new[]{1,120,180})
                foreach(bool replace in new[]{false,true})
                {
                    session.Save.SetRoomFlag(0,0x96,0x80,false);session.DebugTravel(0,0x96);
                    player.WarpTo(new(104.25f,72.5f));player.Face(Vector2I.Up);
                    for(int i=0;i<50&&memory.ReadField("wDisabledObjects")==0;i++)app.Step(1,Vector2.Up,batched:batched);
                    app.Step(cancelAfter,Vector2.Zero,batched:batched);
                    FailIf(memory.ReadField("wDisabledObjects")!=0x81,"Gnarled cancellation must sample the active source sequence.");
                    var successor=new NativeScriptInputControl(memory);
                    if(replace){successor.SetDisabledObjects(0x81);successor.SetMenuDisabled(0x81);}
                    entities.Clear();entities.Clear();
                    byte expected=replace?(byte)0x81:(byte)0;
                    FailIf(memory.ReadField("wDisabledObjects")!=expected || memory.ReadField("wMenuDisabled")!=expected ||
                        memory.ReadField("wDisableWarpTiles")!=0 || !session.Inventory.HasTreasure(0x42) || !session.Save.HasRoomFlag(0,0x96,0x80),
                        "Gnarled cancellation must release only its own locks and retain key$42/flag$80.");
                    successor.Cancel();app.Step(4,Vector2.Down,batched:batched);
                    session.DebugTravel(0,0x95);
                }
            }
            finally { root.Free(); }
        }
    }

    private static void CompareSeasonsGnarledStonePixels(BraceletRom native,LinkCollisionRom graphics,
        NpcCharacter actor,int slot,string context)
    {
        var frames=SomariaPrivate<List<NpcCharacterAnimationFrame>>(actor,"_scriptAnimation");
        var frame=frames[actor.CurrentAnimationFrame];
        CompareSeasonsInteractionPixels(native,graphics,frame.Texture,frame.Offset,slot,context);
    }

    private static void CompareSeasonsInteractionPixels(BraceletRom native,LinkCollisionRom graphics,
        Texture2D texture,Vector2 offset,int slot,string context,int? tileBase=null)
    {
        using Image actual=texture.GetImage();
        var source=ValidationRom.LoadCleanUs(OracleGame.Seasons);
        int pointer=native.ReadNativeByte(slot+0x1e)|(native.ReadNativeByte(slot+0x1f)<<8);
        int at=(0x12+(pointer>>14))*0x4000+(pointer&0x3fff),count=source.Span[at];
        int width=actual.GetWidth(),height=actual.GetHeight();
        var expected=new Color[width*height];
        // Decode executed OAM against native-loaded VRAM/palettes, independently of generated PNGs.
        for(int cell=count-1;cell>=0;cell--)
        {
            int row=at+1+cell*4,flags=source.Span[row+3]^native.ReadNativeByte(slot+0x1c);
            int tile=(source.Span[row+2]+(tileBase??native.ReadNativeByte(slot+0x1d)))&0xfe;
            int originX=unchecked((sbyte)source.Span[row+1])-8-(int)offset.X;
            int originY=unchecked((sbyte)source.Span[row])-16-(int)offset.Y;
            FailIf(originX<0 || originX+8>width || originY<0 || originY+16>height,
                $"{context}: native OAM exceeds sprite bounds/offset.");
            for(int y=0;y<16;y++)for(int x=0;x<8;x++)
            {
                int sourceY=(flags&0x40)==0?y:15-y,bit=(flags&0x20)==0?7-x:x;
                int gfx=0x8000+tile*16+sourceY*2,bank=(flags>>3)&1;
                int shade=((graphics.VramByte(bank,gfx)>>bit)&1)|(((graphics.VramByte(bank,gfx+1)>>bit)&1)<<1);
                if(shade==0)continue;
                int colorAt=0xdec0+(flags&7)*8+shade*2;
                int rgb=graphics.BankByte(2,colorAt)|(graphics.BankByte(2,colorAt+1)<<8);
                expected[(originY+y)*width+originX+x]=new((rgb&31)/31f,((rgb>>5)&31)/31f,((rgb>>10)&31)/31f);
            }
        }
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            Color observed=actual.GetPixel(x,y),wanted=expected[y*width+x];
            FailIf(Math.Abs(observed.A-wanted.A)>1f/255 || wanted.A!=0 &&
                (Math.Abs(observed.R-wanted.R)>1f/255 || Math.Abs(observed.G-wanted.G)>1f/255 || Math.Abs(observed.B-wanted.B)>1f/255),
                $"{context}: sprite pixel({x},{y})={observed}, clean-US OBJ={wanted}.");
        }
    }
}
