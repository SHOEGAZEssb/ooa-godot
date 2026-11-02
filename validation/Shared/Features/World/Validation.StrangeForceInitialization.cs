
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckStrangeForceInitialization(FrontendValidationRoot root, bool batched)
    {
        // Only Ages' initializeRoom has this early return. Seasons' ordinary
        // static/remembered construction is independently exercised above.
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        if (rooms.RuntimeState.Game != OracleGame.Ages) return;
        var entities = root._entities;
        var memory = rooms.RuntimeState;
        var data = StaticObjectState.Data(memory);
        int group = rooms.ActiveGroup, roomId = rooms.CurrentRoom.Id;
        byte[] buffer = Enumerable.Range(0,data.Slots*8).Select(offset=>memory.ReadWramByte(data.Start+offset)).ToArray();
        byte force = memory.ReadField("wSentBackByStrangeForce"), modifier = memory.ReadField("wRoomStateModifier");
        int initializing = 0;
        void ObserveInitialization(int _, OracleRoomData __) => initializing++;
        var app = new ApplicationValidationFixture(root);
        try
        {
            root.LoadDebugRoom(0,0x45);
            var room = rooms.CurrentRoom;
            entities.Clear();
            var native = new LinkCollisionRom(bankedStack:true);
            for (int slot=2;slot<16;slot++)
                for (int offset=0;offset<0x40;offset++) memory.SetWramByte(0xd040+slot*0x100+offset,0);
            StaticObjectState.Clear(memory);
            StaticObjectState.Write(memory,new(0,3,room.Id,0x7e,0x5a,24,40,"Strange Force static sentinel"));
            for(int offset=0;offset<data.Slots*8;offset++) native[data.Start+offset]=memory.ReadWramByte(data.Start+offset);
            memory.SetField("wSentBackByStrangeForce",1); native[0xcdde]=1;
            memory.SetField("wRoomStateModifier",0x5a); native[0xcc32]=0x5a;
            native[0xcc30]=(byte)room.Id;
            var seed=entities.Random.CaptureState(); native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
            entities.RoomInitializing+=ObserveInitialization;
            try { entities.LoadRoom(rooms.ActiveGroup,room); }
            finally { entities.RoomInitializing-=ObserveInitialization; }
            native.Call(0x30fe,bank:1); // compiled clean-US initializeRoom, including its checked tail allocation
            var actor=entities.EntityAdapters<ScreenDistortionRoomEntity>().Single();
            FailIf(entities.EntityAdapters<IRoomEntity>().Count()!=1 || entities.NativeInteractionSlot(actor)!=2 ||
                initializing!=0 || memory.ReadField("wRoomStateModifier")!=0x5a ||
                entities.Random.CaptureState().Calls!=seed.Calls || native.RandomUpdates!=0,
                "Strange Force: ordinary room code, placed/static content and placement RNG must be bypassed at initializeRoom itself.");
            for(int offset=0;offset<data.Slots*8;offset++)
                FailIf(memory.ReadWramByte(data.Start+offset)!=native[data.Start+offset],
                    "Strange Force: early return must retain the static buffer, including an otherwise unsupported matching record.");
            for(int offset=0;offset<0x40;offset++)
                FailIf(memory.ReadWramByte(0xd240+offset)!=native[0xd240+offset],
                    $"Strange Force: native construction byte${0xd240+offset:x4} differs before state zero.");
            root._sound.ClearPlayRequestAudit();
            FailIf(root._sound.PlayRequestsFor(SoundId.SndWarpStart)!=0 || !actor.NativeInitializationPending,
                "Strange Force construction must not execute state zero or play its cue.");
            bool rejected=false;
            try { _=root.CaptureDebugSavestate(); }
            catch(NotSupportedException error) { rejected=error.Message.Contains("INTERAC $7c"); }
            FailIf(!rejected || engine.CanCaptureDebugState,
                "Strange Force: direct capture and application admission must reject the unreconstructed LCD effect.");
            rejected=false;
            try { entities.ValidateDebugReconstruction(memory.CaptureState(),rooms.ActiveGroup,room.Id); }
            catch(NotSupportedException error) { rejected=error.Message.Contains("INTERAC $7c"); }
            FailIf(!rejected,"Strange Force: restored native $7c must be rejected before replacing live owners.");
            memory.SetField("wScrollMode",1); native[0xcd00]=1;
            memory.SetField("wDisabledObjects",2); native[0xcc8a]=2;
            root._scene.Dialogue.ShowMessage("Distortion pause.",104); native[0xcba0]=1;
            int updates=0;
            void Step(int count) => app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native[0xcc00]=memory.FrameCounter;
                native.Call(0x3b36,bank:0,restoresBank:false); // real interaction dispatch, not direct event stepping
                for(int offset=0;offset<0x40;offset++)
                    FailIf(memory.ReadWramByte(0xd240+offset)!=native[0xd240+offset],
                        $"Strange Force/batch{batched}/update{updates}: native interaction byte${0xd240+offset:x4} differs.");
                FailIf(memory.ReadField("wGfxRegs2.LYC")!=native[0xc490] ||
                    root._sound.PlayRequestsFor(SoundId.SndWarpStart)!=native.SoundRequests.Count(cue=>cue==SoundId.SndWarpStart),
                    "Strange Force: state zero must publish the LCD origin and cue once despite text/category masks.");
                if(updates++==0) return;
                for(int line=0;line<256;line++)
                    FailIf(memory.ReadWramByte(0xc300+line)!=native[0xc300+line],
                        $"Strange Force: native wave sample${line:x2} differs at frame${memory.FrameCounter:x2}.");
                FailIf(!SomariaPrivate<bool>(root._scene.RoomView,"_waveActive") ||
                    SomariaPrivate<int>(root._scene.RoomView,"_wavePhase")!=(memory.FrameCounter&0x7f),
                    "Strange Force: the shared session must bind the native interaction's wave to its room presenter.");
            });
            Step(1); Step(4);
            root._scene.Dialogue.Close(); native[0xcba0]=0;
            memory.SetField("wScrollMode",8); native[0xcd00]=8;
            Step(4);
            FailIf(root._sound.PlayRequestsFor(SoundId.SndWarpStart)!=1 || entities.Random.CaptureState().Calls!=seed.Calls,
                "Strange Force: repeated always-update visits must not replay initialization or consume RNG.");
            // Retain a completely occupied outgoing interaction pool. Failed
            // distortion admission still takes the native early return.
            memory.SetField("wSentBackByStrangeForce",0); memory.SetField("wScrollMode",1);
            StaticObjectState.Clear(memory); entities.LoadRoom(rooms.ActiveGroup,room);
            while(entities.InteractionSlotAvailable)
            {
                var puff=new StationaryDebrisEffect(); puff.Initialize(room.AssetRoot,new(24,24),SoundId.MusNone);
                entities.AddEntity(new InteractionSlotValidationEntity(puff,_=>{}));
            }
            for(int slot=2;slot<16;slot++) {native[0xd040+slot*0x100]=1;native[0xd041+slot*0x100]=0x5f;}
            seed=entities.Random.CaptureState(); memory.SetField("wSentBackByStrangeForce",1);
            root._sound.ClearPlayRequestAudit();
            entities.BeginScreenTransition(rooms.ActiveGroup,room,new Vector2(160,0),root._scene.Player);
            native.Call(0x30fe,bank:1);
            FailIf(entities.EntityAdapters<IRoomEntity>().Any() || entities.Random.CaptureState().Calls!=seed.Calls ||
                root._sound.PlayRequestsFor(SoundId.SndWarpStart)!=0,
                "Strange Force: a full retained pool must skip the effect without falling through to normal construction/RNG/cues.");
            entities.FinishScreenTransition();
        }
        finally
        {
            root._scene.Dialogue.Close(); memory.SetField("wSentBackByStrangeForce",force);
            memory.SetField("wRoomStateModifier",modifier); memory.SetField("wDisabledObjects",0); memory.SetField("wScrollMode",1);
            for(int offset=0;offset<buffer.Length;offset++) memory.SetWramByte(data.Start+offset,buffer[offset]);
            root.LoadDebugRoom(group,roomId); root._scene.RoomView.ClearHorizontalWave();
        }
        FailIf(!engine.CanCaptureDebugState,"Strange Force: retiring the effect must release debug reconstruction admission.");
    }
}
