
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriDepartureGatesRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteField("wDimitriState",0x23);
            LoadValidationRoom(0,0x89);_entities.Clear();
            _transitions.BeginValidationScroll(_player,Vector2I.Down,0x99);
            FailIf(_runtimeState.ReadWramByte(0xd240)!=1 || _runtimeState.ReadWramByte(0xd244)!=0,
                "Invisible $71:$06 must retain state0 until the native incoming interaction walk.");
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,104,72);
            native[0xcd00]=8;native[0xd240]=1;native[0xd241]=0x71;native[0xd242]=6;
            _runtimeState.SetWramByte(0xd101,0x0c);native[0xd101]=0x0c;
            _runtimeState.SetWramByte(0xd138,4);native[0xd138]=4;
            int updates=0;bool wasScrolling=true;
            while(_transitions.ScrollActive && updates<100)
            {
                StepGameplayUpdates(batched?2:1,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native[0xcd00]=wasScrolling?(byte)8:(byte)1;
                    native.AdvanceInteractions(_entities.FrameCounter);updates++;
                    wasScrolling=_transitions.ScrollActive;
                    foreach(int at in new[]{0xd240,0xd244,0xd103,0xd104,0xcc91,0xcc8a,0xcc02})
                        FailIf(_runtimeState.ReadWramByte(at)!=native[at],$"$71:$06 scrolling update{updates}/batch{batched}: byte${at:x4} differs.");
                    FailIf(_dialogue.IsOpen,"Initialized mainland script must stay frozen during scrolling.");
                    if(updates==3){_runtimeState.SetWramByte(0xd138,0);native[0xd138]=0;}
                });
            }
            FailIf(_transitions.ScrollActive || _runtimeState.ReadWramByte(0xd244)!=1,
                "Scrolling must admit the pending native request and freeze the following script state.");
        }
        foreach(bool batched in new[]{false,true})
        foreach(int mask in new[]{0,2})
        foreach(bool text in new[]{false,true})
        foreach(int gate in new[]{0,1,2})
        {
            var save=OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteField("wDimitriState",gate==0?(byte)0x40:(byte)0x23);
            LoadValidationRoom(0,0x99);_player.WarpTo(new(104,72));
            var slot=_entities.EntityAdapters<DimitriDepartureRoomEntity>().Single();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,104,72);
            native[0xd240]=1;native[0xd241]=0x71;native[0xd242]=6;
            _runtimeState.SetWramByte(0xd101,gate==1?(byte)0x0b:(byte)0x0c);native[0xd101]=gate==1?(byte)0x0b:(byte)0x0c;
            _runtimeState.SetWramByte(0xd138,4);native[0xd138]=4;
            _runtimeState.SetField("wDisabledObjects",(byte)mask);native[0xcc8a]=(byte)mask;
            if(text){_dialogue.ShowGameplayMessage("Earlier text",100);native[0xcba0]=1;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);
                foreach(int at in new[]{0xd240,0xd244,0xd103,0xd104,0xcc91,0xcc8a,0xcc02})
                    FailIf(_runtimeState.ReadWramByte(at)!=native[at],$"$71:$06 gate{gate}/mask${mask:x2}/text{text}/batch{batched}: byte${at:x4} differs.");
            });
            Step(3);
            FailIf(slot.Finished!=(gate!=2),"$71:$06 must delete for completed save/wrong retained ID and wait for water, even with text/category mask.");
            if(gate==2)
            {
                _runtimeState.SetWramByte(0xd138,0);native[0xd138]=0;
                Step(1);
                FailIf(_runtimeState.ReadWramByte(0xd244)!=1 || _runtimeState.ReadWramByte(0xd104)!=0x0a,
                    "$71:$06 tests retained D1.id without an enabled gate and samples live var38.");
            }
        }
        foreach(bool newer in new[]{false,true})
        foreach(bool duringText in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteField("wDimitriState",0x23);
            LoadValidationRoom(0,0x99);_player.WarpTo(new(104,72));
            var slot=_entities.EntityAdapters<DimitriDepartureRoomEntity>().Single();
            _runtimeState.SetWramByte(0xd101,0x0c);
            StepGameplayUpdates(1,Vector2.Zero);
            if(duringText)for(int i=0;i<10 && !_dialogue.IsOpen;i++)StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_dialogue.IsOpen!=duringText,"Departure cancellation fixture must reach its declared native ownership boundary.");
            if(newer)
            {
                // Identical newer writes also supersede the cancelled script.
                _runtimeState.SetField("wDisableScreenTransitions",1);
                if(duringText){_runtimeState.SetField("wDisabledObjects",0x11);_runtimeState.SetField("wMenuDisabled",0x80);}
            }
            _entities.RetireEntity(slot);
            for(int offset=0;offset<64;offset++)FailIf(_runtimeState.ReadWramByte(0xd240+offset)!=0,
                $"$71:$06 cancellation retained slot byte${offset:x2}.");
            FailIf(_runtimeState.ReadWramByte(0xd240)!=0 || _runtimeState.ReadField("wDisableScreenTransitions")!=(newer?1:0) ||
                _runtimeState.ReadField("wDisabledObjects")!=(newer&&duringText?0x11:0) ||
                _runtimeState.ReadField("wMenuDisabled")!=(newer&&duringText?0x80:0),
                "Departure cancellation must release only its own control writes and delete all64 slot bytes.");
            _dialogue.Close();
        }
        foreach(bool initialized in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteField("wDimitriState",0x23);
            LoadValidationRoom(0,0x99);_player.WarpTo(new(104,72));
            var slot=_entities.EntityAdapters<DimitriDepartureRoomEntity>().Single();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,104,72);
            native[0xd240]=1;native[0xd241]=0x71;native[0xd242]=6;
            _runtimeState.SetWramByte(0xd101,0x0c);native[0xd101]=0x0c;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceDeathPrelude();native.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                foreach(int at in new[]{0xd240,0xcc8a,0xcc02,0xcc91})
                    FailIf(_runtimeState.ReadWramByte(at)!=native[at],$"$71:$06 death initialized{initialized}/batch{batched}: byte${at:x4} differs.");
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            if(initialized)
            {
                for(int i=0;i<10 && !_dialogue.IsOpen;i++)Step(1);
                FailIf(!_dialogue.IsOpen,"Death fixture must reach TX_2104 before lethal publication.");
            }
            else
            {
                _runtimeState.SetField("wDisabledObjects",0x11);native[0xcc8a]=0x11;
                _runtimeState.SetField("wMenuDisabled",0x80);native[0xcc02]=0x80;
                _runtimeState.SetField("wDisableScreenTransitions",3);native[0xcc91]=3;
            }
            FailIf(!_player.ApplyDamage(_player.HealthQuarters),"Departure death fixture must publish lethal damage through Link.");
            native.ApplyLinkDamage(unchecked((byte)(-2*native[0xc6aa])));native[0xd004]=3;
            Step(3);
            if(initialized)
            {
                FailIf(slot.Finished,"Text must freeze an initialized $71:$06 before its death-entry gate.");
                _dialogue.Close();native[0xcba0]=0;Step(1);
            }
            FailIf(!slot.Finished || _runtimeState.ReadField("wDisabledObjects")!=0 || _runtimeState.ReadField("wMenuDisabled")!=0x80,
                "$71 death entry clears the object mask and deletes its slot while retaining the menu byte.");
        }
    }

    private void ValidateDimitriDepartureRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int room in new[]{0x98,0x99,0xa8})
        foreach(int direction in new[]{0,1,2,3})
        {
            var save=OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            _saveData.WriteField("wDimitriState",0x23);_saveData.WriteField("wRickyState",0);
            OracleRoomData sourceRoom=_world.LoadRoom(0,room);
            Vector2 landing=(from y in Enumerable.Range(1,6)
                from x in Enumerable.Range(1,8).Reverse()
                let point=new Vector2(x*16,y*16)
                where sourceRoom.GetTerrainInfo(point+new Vector2(0,5)).Hazard==HazardType.None &&
                    Enumerable.Range((int)point.X-5,sourceRoom.Width-(int)point.X+5).All(px=>
                        !sourceRoom.IsSolid(new(px,point.Y-5)) && !sourceRoom.IsSolid(new(px,point.Y+8)))
                select point).First();
            CompanionRuntimeState.BeginAnimalRide(_runtimeState,_saveData,0x0c,landing,direction);
            LoadValidationRoom(0,room);
            var actor=_entities.Entities<DimitriCompanionRoomEntity>().Single();
            // Declared initialized mounted boundary. Room-state-zero and visual
            // initialization are covered separately; all following visits are real.
            actor.RoomInitialization=null;
            var mounted=new CompanionRom(0x0c,landing,direction,_currentRoom);
            var random=_random.CaptureState();
            var native=new SomariaRom(_saveData,random,_currentRoom,direction,(int)landing.X,(int)landing.Y)
                {CompanionDispatchEnabled=true};
            for(int at=0xd000;at<0xd040;at++)native[at]=mounted[at];
            for(int at=0xd100;at<0xd140;at++)native[at]=mounted[at];
            native[0xd13c]=0xff; // companionFinalizeMounting, unlike the generic ROM fixture's nonzero sentinel.
            // Stage the declared mounted boundary identically; later expectations come from native execution.
            for(int at=0xd100;at<0xd140;at++)_runtimeState.SetWramByte(at,native[at]);
            native[0xcc2c]=0xd1;native[0xcc96]=1;native[0xccaa]=0xff;
            // Literal native object prefixes: $74/$71:06, $71:06, $71:01/$71:06.
            int slot=room==0x99?2:3, lane=0xd040+slot*256;
            if(room==0x98){native[0xd240]=1;native[0xd241]=0x74;native[0xd24b]=0x28;native[0xd24d]=0x48;}
            if(room==0xa8){native[0xd240]=1;native[0xd241]=0x71;native[0xd242]=1;}
            native[lane]=1;native[lane+1]=0x71;native[lane+2]=6;
            var controller=_entities.EntityAdapters<DimitriDepartureRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(controller.Node)!=slot,"Mainland $71:$06 must preserve the native preceding allocation.");
            for(int offset=0;offset<64;offset++)FailIf(_runtimeState.ReadWramByte(lane+offset)!=native[lane+offset],
                $"Mainland ${room:x2}: $71:$06 constructor byte${offset:x2} differs.");
            int updates=0;bool textSeen=false,scriptFinished=false;
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count=1)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.UpdateGameplay(0,0,0xff,_saveData.ReadField("wPlaytimeCounter"));
                string context=$"Mainland ${room:x2}/dir{direction}/batch{batched}/update{++updates}";
                for(int at=0xd100;at<0xd140;at++)
                    FailIf(_runtimeState.ReadWramByte(at)!=native[at],
                        $"{context}: D1 byte${at:x4} runtime${_runtimeState.ReadWramByte(at):x2}/native${native[at]:x2}.");
                foreach(int at in new[]{0xd100,0xd103,0xd104,0xd108,0xd109,0xd138,0xd13c,0xcc2c,0xcc8a,0xcc02,0xcc91,0xcbc3,0xcc24,0xcc25,0xcc26,0xcc27,0xcc28})
                    FailIf(_runtimeState.ReadWramByte(at)!=native[at],
                        $"{context}: byte${at:x4} runtime=${_runtimeState.ReadWramByte(at):x2}/native=${native[at]:x2}; phase={actor.Phase}.");
                FailIf(actor.PrecisePosition!=new Vector2(native.Word(0xd10c)/256f,native.Word(0xd10a)/256f) && native[0xd100]!=0,
                    context+": native departure XY differs.");
                FailIf(_player.CompanionRideActive!=(native[0xcc2c]==0xd1) ||
                    _player.CompanionJumpActive!=(native[0xcc2c]==0xd0 && native[0xcc5c]!=0) ||
                    _player.Position!=new Vector2(native[0xd00d],native[0xd00b]),context+": native dismount/air/Link position differs.");
                if(native[0xcc2c]==0xd0)
                    FailIf(_player.LocalRespawnPosition!=new Vector2(native[0xcc22],native[0xcc21]) ||
                        _player.LocalRespawnFacingVector!=(Vector2I)OracleObjectMath.StrictCardinalVector(native[0xcc23]*8),
                        context+": native dismount local-respawn publication differs.");
                FailIf(_runtimeState.ReadWramByte(lane)!=(native[lane]),context+": script lifetime differs.");
                if(_dialogue.IsOpen)
                {
                    textSeen=true;
                    FailIf(native.Word(0xcba2)-0x400!=0x2104 ||
                        _dialogue.CurrentMessage!=DialogueBox.PlainText(new AgesDimitriStoryDatabase().Text(0x2104)),
                        context+": native TX_2104 missing.");
                    foreach(int offset in new[]{0x1e,0x1f,0x20,0x21,0x22,0x23})
                        FailIf(_runtimeState.ReadWramByte(lane+offset)!=native[lane+offset],context+$": facing field${offset:x2} differs.");
                }
                if(native[lane]==0)scriptFinished=true;
                // This fixture executes object code, not text rendering. TX_2104
                // contains its own $c4 cue; its source token is asserted below.
                var objectSounds=sounds.Requests.Where(cue=>cue!=SoundId.SndText).ToList();
                if(textSeen && objectSounds.Count(s=>s==0xc4)>native.Sounds.Count(s=>s==0xc4))objectSounds.Remove(0xc4);
                FailIf(!objectSounds.SequenceEqual(native.Sounds),
                    context+$": cues runtime=[{string.Join(',',sounds.Requests.Select(s=>s.ToString("x2")))}]/native=[{string.Join(',',native.Sounds.Select(s=>s.ToString("x2")))}].");
                OracleRandomState current=_random.CaptureState();
                FailIf(current.Rng1!=native[0xff94] || current.Rng2!=native[0xff95] || current.Calls-random.Calls!=native.RandomCalls,
                    context+": native shared RNG differs.");
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            Step();
            FailIf(_runtimeState.ReadWramByte(lane+4)!=1 || _runtimeState.ReadWramByte(0xd103)!=2 ||
                !_player.CompanionRideActive || _dialogue.IsOpen,"$71:$06 state0 must yield its D1 request until the following special-object visit.");
            for(int i=0;i<100 && !_dialogue.IsOpen;i++)Step();
            FailIf(!textSeen || scriptFinished || _player.CompanionJumpActive,"Departure must wait for Link's actual landing before TX_2104.");
            FailIf(!new AgesDimitriStoryDatabase().Text(0x2104).StartsWith("\\sfx(0xc4)We're here! You\ngo alone now.\nBye!"),
                "TX_2104 must retain the source's $c4 text cue and mainland farewell.");
            Step(5); // Text freezes the initialized interaction, retaining masks.
            _dialogue.Close();native[0xcba0]=0;
            Step();
            FailIf(!scriptFinished || _runtimeState.ReadWramByte(0xd103)!=3 || _runtimeState.ReadField("wDisabledObjects")!=0x11,
                "Script completion hands control to the body without clearing its $11 mask.");
            FailIf((_runtimeState.ReadField("wStatusBarNeedsRefresh")&2)!=(native[0xcbe9]&2),"$71:$06 script end must request status-bar bit1.");
            byte[] held=Enumerable.Range(0xd100,64).Select(_runtimeState.ReadWramByte).ToArray();
            _runtimeState.SetField("wDisabledObjects",0x31);native[0xcc8a]=0x31;
            Step(3);
            FailIf(!Enumerable.Range(0xd100,64).Select(_runtimeState.ReadWramByte).SequenceEqual(held) ||
                !_entities.Entities<DimitriCompanionRoomEntity>().Any(body=>ReferenceEquals(body,actor)),
                "Ages stateA must retain all64 native bytes and the same physical presenter through an admission hold.");
            _runtimeState.SetField("wDisabledObjects",0x11);native[0xcc8a]=0x11;
            Step(); // var03=$03 selects animation/cue without moving.
            if(room==0x98 && direction==1)
            {
                Vector2 probe=actor.Position+new Vector2(0,5);
                byte original=_currentRoom.GetMetatile(probe),collision=_currentRoom.GetTerrainInfo(probe).Collision;
                int packed=_currentRoom.GetPackedPosition(probe);
                foreach(var hazard in new[]{HazardType.Water,HazardType.Hole})
                {
                    byte tile=(byte)Enumerable.Range(0,256).First(t=>TilePropertyDatabase.For(AgesGameContent.AssetRoot).Hazard(_currentRoom.ActiveCollisions,(byte)t)==hazard);
                    _currentRoom.SetPositionTileAndCollision(probe,tile,0,0);native[0xcf00+packed]=tile;native[0xce00+packed]=0;
                    _runtimeState.SetWramByte(0xd105,3);_runtimeState.SetWramByte(0xd106,0x55);native[0xd105]=3;native[0xd106]=0x55;
                    Step();
                    FailIf(_runtimeState.ReadWramByte(0xd105)!=native[0xd105] || _runtimeState.ReadWramByte(0xd106)!=native[0xd106] ||
                        native[0xd138]!=4,"State A must retain hazard helper writes and water across the following hole.");
                }
                _currentRoom.SetPositionTileAndCollision(probe,original,collision,0);native[0xcf00+packed]=original;native[0xce00+packed]=collision;
            }
            for(int i=0;i<160 && !actor.Finished;i++)Step();
            FailIf(!actor.Finished || _runtimeState.ReadField("wDisabledObjects")!=0 || _runtimeState.ReadField("wMenuDisabled")!=0,
                "Native offscreen departure must delete D1 and release its controls.");
            for(int offset=0;offset<64;offset++)FailIf(_runtimeState.ReadWramByte(0xd100+offset)!=0 || native[0xd100+offset]!=0,
                $"Native departure retained D1 byte${offset:x2}.");
            Step(3);
        }
        GD.Print("Validated native mainland $71:$06 placement, D1/Link/interaction handoff, landing, facing, text, retained control, departure/deletion and RNG/cues through split/batched gameplay.");
    }
}
