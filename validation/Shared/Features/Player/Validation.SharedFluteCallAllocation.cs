
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedFluteCallAllocation(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var player=root._scene.Player;
        var inventory=player.Inventory;
        var memory=rooms.RuntimeState;
        var instruments=((PlayerWorld)player.World).Instruments;
        var game=memory.Game;
        var app=new ApplicationValidationFixture(root);
        FailIf((rooms.CurrentRoom.TilesetFlags&0x81)!=1,"Flute allocation requires the preceding actual outdoor floor fixture.");
        foreach(bool full in new[]{true,false})
        for(int repeat=0;repeat<2;repeat++)
        {
            player.ClearNativeItemParents(); entities.Clear();
            inventory.GiveTreasure(TreasureId.Flute,0); inventory.EquipA(TreasureId.Flute); inventory.EquipB(0);
            rooms.SaveData.WriteField("wFluteIcon",0);
            player.WarpTo(new(88,72)); player.Face(Vector2I.Down);
            app.Step(1,Vector2.Zero,batched:batched);
            int updates=0;
            byte[]? runtimeHeader=null,nativeHeader=null;
            var order=new List<(int Slot,bool Text)>();
            var probes=new List<FluteCallPhaseProbe>();
            for(int index=0;index<14;index++)
            {
                int slot=index+2;
                var probe=new FluteCallPhaseProbe(memory,()=>
                {
                    if(updates!=94 || slot is not (2 or 4)) return;
                    order.Add((slot,root.DialogueOpen));
                    if(!full && slot==2)
                        runtimeHeader=Enumerable.Range(0,64).Select(offset=>memory.ReadWramByte(0xd340+offset)).ToArray();
                });
                probes.Add(probe); entities.AddEntity(probe);
            }
            FailIf(entities.InteractionSlotAvailable,"Fourteen live native probes must fill the interaction pool.");
            var native=new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,2,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation(); native[0xcc2d]=(byte)rooms.ActiveGroup;
            for(int page=0xd2;page<=0xdf;page++)
            for(int offset=0;offset<0x40;offset++) native[(page<<8)+0x40+offset]=memory.ReadWramByte((page<<8)+0x40+offset);
            var seed=entities.Random.CaptureState(); native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
            int notes=instruments.NoteSpawnCount;
            var sound=root._sound.AttachPlayRequestAudit();
            void Step(int count,int keys)
            {
                app.Step(count,Vector2.Zero,held:MenuRomActions(keys),pressed:updates==0?MenuRomActions(keys):[],batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,keys,interactions:true,beforeInteractions:()=>
                    {
                        if(!full && updates==94) nativeHeader=Enumerable.Range(0,64).Select(offset=>native[0xd340+offset]).ToArray();
                    });
                    updates++;
                    var now=entities.Random.CaptureState();
                    FailIf(now.Rng1!=native[0xff94] || now.Rng2!=native[0xff95] || now.Calls-seed.Calls!=native.RandomCalls,
                        $"{game}: full-pool Flute RNG differs at update{updates}.");
                    FailIf(player.IsUsingHarp!=(native[0xd500]!=0),$"{game}: full-pool Flute parent lifetime differs.");
                });
            }
            Step(94,1);
            foreach(int offset in new[]{0x0b,0x0d,0x0f})
            {
                byte value=(byte)(0x51+offset);
                memory.SetWramByte(0xd500+offset,value); native[0xd500+offset]=value;
            }
            if(!full)
            {
                entities.RetireEntity(probes[1]);
                for(int offset=0;offset<64;offset++)
                {
                    byte value=offset==0?(byte)0:unchecked((byte)(0x31+offset));
                    memory.SetWramByte(0xd340+offset,value); native[0xd340+offset]=value;
                }
            }
            Step(1,1);
            FailIf(root.DialogueOpen==full || (native[0xcba0]!=0)==full || player.IsUsingHarp ||
                entities.EntityAdapters<CompanionFluteSpawner>().Any(),$"{game}: Flute caller must obey allocation failure and delete after one dispatch.");
            FailIf(Enumerable.Range(0,64).Any(offset=>memory.ReadWramByte(0xd500+offset)!=0 || native[0xd500+offset]!=0),
                $"{game}: Flute completion must delete all64 parent bytes even when the interaction pool is full.");
            FailIf(instruments.NoteSpawnCount!=notes || native.RandomCalls!=2 || entities.Random.Calls-seed.Calls!=2,
                $"{game}: failed note allocations must retain both parent RNG draws.");
            FailIf(!sound.Requests.Where(id=>id!=SoundId.SndText).SequenceEqual(native.Sounds),
                $"{game}: Flute allocation changed playback cues.");
            FailIf(!order.SequenceEqual(new[]{(2,false),(4,!full)}),
                $"{game}: Flute text must appear between earlier slot$02 and later slot$04, not in Link's phase.");
            if(!full)
            {
                FailIf(runtimeHeader is null || nativeHeader is null || !runtimeHeader.SequenceEqual(nativeHeader) ||
                    runtimeHeader[0]!=1 || runtimeHeader[1]!=(game==OracleGame.Ages?0x67:0x5f) || runtimeHeader[2]!=0x80,
                    $"{game}: native Flute constructor must retain every untouched byte in its first-free slot$03: "+
                    (runtimeHeader is null || nativeHeader is null ? "missing capture" : string.Join(",",Enumerable.Range(0,64)
                        .Where(offset=>runtimeHeader[offset]!=nativeHeader[offset])
                        .Select(offset=>$"${offset:x2}=${runtimeHeader[offset]:x2}/${nativeHeader[offset]:x2}"))));
                FailIf(Enumerable.Range(0,64).Any(offset=>memory.ReadWramByte(0xd340+offset)!=native[0xd340+offset]) ||
                    native.Word(0xcba2)!=0x550f || root._scene.Dialogue.CurrentMessage!="Your song just\nechoes...",
                    $"{game}: Flute caller deletion or native TX_510f differs.");
                root._scene.Dialogue.Close(); native[0xcba0]=0;
            }
            entities.Clear();
            app.Step(1,Vector2.Zero,batched:batched);
        }
        // bank0.updateInteractions admits state0 or enabled bit7 under text/$02.
        foreach(bool text in new[]{false,true})
        foreach(var gate in new[]{(State:0,Enabled:1),(State:1,Enabled:1),(State:1,Enabled:0x81)})
        {
            entities.Clear(); player.ClearNativeItemParents();
            var native=new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,2,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            instruments.Complete(player,0xff);
            memory.SetWramByte(0xd240,(byte)gate.Enabled); memory.SetWramByte(0xd244,(byte)gate.State);
            native[0xd240]=(byte)gate.Enabled; native[0xd241]=(byte)(game==OracleGame.Ages?0x67:0x5f);
            native[0xd242]=0x80; native[0xd244]=(byte)gate.State;
            memory.SetField("wDisabledObjects",2); native[0xcc8a]=2;
            if(text)
            {
                root._scene.Dialogue.ShowMessage("Existing Flute message.",player.Position.Y);
                native[0xcba0]=1; native[0xcba2]=0x11; native[0xcba3]=0x55;
            }
            app.Step(1,Vector2.Zero,batched:batched,afterUpdate:()=>native.UpdateLinkItems(entities.FrameCounter,0,interactions:true));
            bool waiting=gate.State!=0 && (gate.Enabled&0x80)==0;
            FailIf(memory.ReadWramByte(0xd240)!=native[0xd240] || (memory.ReadWramByte(0xd240)!=0)!=waiting ||
                root.DialogueOpen!=(text || !waiting),$"{game}: native Flute state/always-update/text gate differs.");
            if(text)
                FailIf(root._scene.Dialogue.CurrentMessage!="Existing Flute message." || native.Word(0xcba2)!=0x5511,
                    $"{game}: Flute caller replaced an already active message.");
            root._scene.Dialogue.Close(); native[0xcba0]=0;
            memory.SetField("wDisabledObjects",0); native[0xcc8a]=0;
            app.Step(1,Vector2.Zero,batched:batched,afterUpdate:()=>native.UpdateLinkItems(entities.FrameCounter,0,interactions:true));
            FailIf(Enumerable.Range(0,64).Any(offset=>memory.ReadWramByte(0xd240+offset)!=0 || native[0xd240+offset]!=0) ||
                root.DialogueOpen!=waiting,$"{game}: Flute caller must retire on its next eligible visit.");
            root._scene.Dialogue.Close(); entities.Clear();
        }
    }
}
