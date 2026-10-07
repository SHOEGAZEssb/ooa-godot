using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterPalette()
    {
        // Isolate the actual room4:9d shutter on unchanged floor. Its trigger
        // producer is a declared input; commands select state2/state3 through
        // the original script, without injecting either animation state.
        (DungeonDoorRoomEntity Door, SomariaRom Rom, OracleRandomState Seed) Prepare()
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var source = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e);
            var door = new DungeonDoorRoomEntity(source,_currentRoom,data,() => 0,_entities.TriggerIsActive,
                p => p-new Vector2(80,48),() => (long)_animationTicks,_sound.PlaySound,
                default,true,_rooms.TrySetTile,_entities.UpdateBossShutterSignal,
                paletteFadeActive:() => _entities.DoorPaletteFadeActive);
            _entities.AddEntity(door); _entities.SetTrigger(0,true);
            Vector2 start = new(184.25f,104.5f); _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start),"Shutter palette fixture requires unchanged room4:9d floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,184,104);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80; rom[0xcca0] = 1;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            return (door,rom,seed);
        }
        int fixture = 0;
        foreach (bool opening in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (door,rom,seed) = Prepare();
            Func<bool> previous = _entities.PaletteFadeActiveSource;
            bool fading = false; _entities.PaletteFadeActiveSource = () => fading;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batch,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                var random = _random.CaptureState();
                FailIf(door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    _entities.BossEntrySignal != rom[0xcc93] || _entities.ActiveTriggers != rom[0xcca0] ||
                    random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),"Native shutter palette admission/counter/shared signals/cues/RNG differ.");
            });
            void Fade(bool active) { fading = active; rom[0xc4ab] = active ? (byte)1 : (byte)0; }
            try
            {
                for (int wait = 0; rom[0xd244] != 2 && wait < 20; wait++) Step();
                FailIf(rom[0xd244] != 2 || rom[0xd245] != 0,"Original trigger script must select opening before the fade input.");
                if (!opening)
                {
                    Step(7); _entities.SetTrigger(0,false); rom[0xcca0] = 0;
                    for (int wait = 0; rom[0xd244] != 3 && wait < 10; wait++) Step();
                    FailIf(rom[0xd244] != 3 || rom[0xd245] != 0,"Original inactive-trigger script must select closing before the fade input.");
                }
                Fade(true); Step(3);
                FailIf(SomariaPrivate<int>(door,"_counter") != (opening ? 0 : 4) ||
                    SomariaPrivate<DoorState>(door,"_state") != (opening ? DoorState.ReadyToOpen : DoorState.ClosingInterleaved),
                    "An external palette mode must hold opening and permit closing's first three updates.");
                if (opening)
                {
                    Fade(false); Step(3);
                    FailIf(SomariaPrivate<int>(door,"_counter") != 4,"Opening must begin its six-update counter when the fade clears.");
                    Fade(true); Step(3);
                    FailIf(SomariaPrivate<int>(door,"_counter") != 4 || !_currentRoom.IsSolid(door.Position),
                        "A second palette mode must hold the existing interleave and collision.");
                    Fade(false);
                }
                Step(3);
                FailIf(SomariaPrivate<int>(door,"_counter") != 1,"Palette release must retain the final shutter counter update.");
                Step();
                FailIf(_currentRoom.IsSolid(door.Position) == opening,"Opening must finish after the fade; closing must finish during it.");
                Fade(false); Step(3);
            }
            finally { _entities.PaletteFadeActiveSource = previous; }
        }
        fixture = 0;
        foreach (var (delayed,closing) in new (bool Delayed,bool Closing)[] { (false,false),(true,false),(false,true),(true,true) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (sourceDoor,rom,_) = Prepare();
            var sounds = _sound.AttachPlayRequestAudit();
            void InitializeStep() => StepSomariaMotionRom(rom,1,batch);
            for (int wait = 0; rom[0xd244] != 2 && wait < 20; wait++) InitializeStep();
            FailIf(rom[0xd244] != 2 || rom[0xd245] != 0,"Source script must select opening before its live warp fade.");
            if (closing)
            {
                for (int tick = 0; tick < 7; tick++) InitializeStep();
                _entities.SetTrigger(0,false); rom[0xcca0] = 0;
                for (int wait = 0; rom[0xd244] != 3 && wait < 10; wait++) InitializeStep();
                FailIf(rom[0xd244] != 3 || rom[0xd245] != 0,"Source script must select closing before its live warp fade.");
                for (int tick = 0; tick < 3; tick++) InitializeStep();
                FailIf(SomariaPrivate<int>(sourceDoor,"_counter") != 4 || rom[0xd246] != 4,
                    "Live warp must begin during the actual six-update closing interleave.");
            }
            DoorState heldState = SomariaPrivate<DoorState>(sourceDoor,"_state");
            int heldCounter = SomariaPrivate<int>(sourceDoor,"_counter");
            // applyWarpTransition2 bit7 selects fadeoutToWhiteWithDelay(4).
            // The destination is the actual PART$09 button tile, whose PART
            // update selects opening during the live arrival palette thread.
            var warp = new Warp(4,0x9d,-1,0,WarpSourceTransition.FadeOut,4,0x9d,0x8c,0,WarpDestinationTransition.Basic);
            if (delayed) _transitions.ApplyWarpWithDelayedFadeOut(_player,warp);
            else _transitions.ApplyWarpWithFadeOut(_player,warp);
            rom[0xcc47] = 0x84; rom[0xcc48] = 0x9d; rom[0xcc49] = 0; rom[0xcc4a] = 0x8c;
            // initializeGame sets this persistent parser mask to$ff.
            // Bit0 admits parseObjectData; bit1 admits nested room pointers.
            rom[0xcc05] = 0xff;
            rom[0xcc4b] = delayed ? (byte)0x83 : (byte)3; rom.ApplyRequestedWarp();
            DungeonDoorRoomEntity door = sourceDoor; int slot = 0xd240,update = 0;
            int nativeFrame = _entities.FrameCounter; bool loaded = false;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                nativeFrame = (nativeFrame+1)&0xff;
                rom[0xcc00] = (byte)nativeFrame;
                rom.AdvanceWarpPalette();
                if (rom[0xc2ef] == 3)
                {
                    rom.UpdateFadeOutWarp();
                    if (rom[0xc2ef] != 3)
                    {
                        loaded = true; rom.HostilePartsEnabled = true;
                        door = _entities.Entities<DungeonDoorRoomEntity>().Single();
                        int[] slots = Enumerable.Range(0xd2,0x0e).Select(page => (page<<8)|0x40)
                            .Where(address => rom[address] != 0 && rom[address+1] == 0x1e).ToArray();
                        FailIf(slots.Length != 1,$"Native reload shutter allocation differs: group/room=${rom[0xcc2d]:x2}:${rom[0xcc30]:x2}, parser=${rom[0xcc05]:x2}, stateModifier=${rom[0xcc4e]:x2}, cutscene=${rom[0xc2ef]:x2}, slots="+
                            string.Join(",",Enumerable.Range(0xd2,0x0e).Select(page => $"{page:x2}:{rom[(page<<8)|0x40]:x2}/{rom[(page<<8)|0x41]:x2}/{rom[(page<<8)|0x42]:x2}")));
                        slot = slots[0];
                        FailIf(ReferenceEquals(sourceDoor,door),"Native warp reload must replace the source shutter.");
                        FailIf(SomariaPrivate<DoorState>(door,"_state") != DoorState.Initialize || rom[slot+4] != 0,
                            "CUTSCENE_03's load update must leave the destination shutter pending until the following object pass.");
                    }
                }
                else rom.UpdateGameplay(0,0,0xff,nativeFrame);
                rom.AdvanceTileGraphics();
                string context = $"Shutter warp delayed={delayed}, closing={closing}, batch={batch}, update={++update}";
                FailIf(_entities.FrameCounter != nativeFrame ||
                    _rooms.ActiveGroup != rom[0xcc2d] || _currentRoom.Id != rom[0xcc30] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    _transitions.PaletteFadeActive != (rom[0xc4ab] != 0) ||
                    !Mathf.IsEqualApprox(_warpFade.Color.A,Math.Min((int)rom[0xc2ff],31)/31f) ||
                    SomariaPrivate<int>(door,"_counter") != rom[slot+6] ||
                    _currentRoom.GetMetatile(door.Position) != rom[0xcfa7] ||
                    _currentRoom.GetTerrainInfo(door.Position).Collision != rom[0xcea7],
                    context+$": native room/Link/palette/shutter differ; counter={SomariaPrivate<int>(door,"_counter")}/{rom[slot+6]}, state={SomariaPrivate<DoorState>(door,"_state")}/${rom[slot+4]:x2}:${rom[slot+5]:x2}.");
                FailIf(!loaded && (SomariaPrivate<DoorState>(sourceDoor,"_state") != heldState ||
                    SomariaPrivate<int>(sourceDoor,"_counter") != heldCounter),
                    context+": CUTSCENE_03 must omit the entire source object pass, including palette-independent closing.");
                // Complete incoming enemy AI, preload RNG and rendering are
                // outside this bounded reload/palette/shutter comparison.
                bool DoorCue(int cue) => cue is SoundId.SndDoorClose or SoundId.SndSolvePuzzle;
                FailIf(!sounds.Requests.Where(DoorCue).SequenceEqual(rom.Sounds.Where(DoorCue)),context+
                    $": ordered shutter/solve cues differ: runtime={string.Join(",",sounds.Requests.Where(DoorCue))}, native={string.Join(",",rom.Sounds.Where(DoorCue))}, state={SomariaPrivate<DoorState>(door,"_state")}/${rom[slot+4]:x2}:${rom[slot+5]:x2}, script=${rom.Word(slot+0x18):x4}.");
            });
            Step(delayed ? 124 : 31);
            FailIf(loaded || SomariaPrivate<DoorState>(sourceDoor,"_state") != heldState,
                "Native source shutter must retain its selected script/interleave through every nonterminal fade-out update.");
            Step();
            FailIf(!loaded || !_transitions.PaletteFadeActive || _player.PrecisePosition != new Vector2(200,136),
                "The terminal 32/125-update fade must reload onto the original button and begin arrival fading.");
            Step(32);
            FailIf(!_transitions.PaletteFadeActive || SomariaPrivate<DoorState>(door,"_state") != DoorState.ReadyToOpen ||
                (rom[0xcca0]&1) == 0,"Actual button pressure must select opening while all 32 nonterminal arrival updates hold it.");
            Step();
            FailIf(_transitions.PaletteFadeActive || SomariaPrivate<int>(door,"_counter") != 6,
                "The terminal 33rd palette update must release opening in the same gameplay object pass.");
            Step(5);
            FailIf(SomariaPrivate<int>(door,"_counter") != 1 || !_currentRoom.IsSolid(door.Position),
                "Native arrival must retain the complete six-update collision delay.");
            Step(); Step(3);
            FailIf(_currentRoom.IsSolid(door.Position),"Actual button-driven arrival must finish opening after palette release.");
        }
        foreach (bool batch in RomHostSchedules(0))
        {
            var (door,rom,_) = Prepare();
            var sounds = _sound.AttachPlayRequestAudit();
            void InitializeStep() => StepSomariaMotionRom(rom,1,batch);
            for (int wait = 0; rom[0xd244] != 2 && wait < 20; wait++) InitializeStep();
            var data = new DarkRoomDatabase();
            var record = data.GetRoomRecords(5,0xed).Single(row => row.Kind == DarkRoomDatabaseObjectKind.Handler);
            var fade = new DarkRoomState(_currentRoom,data);
            _entities.AddEntity(new DarkRoomHandlerRoomEntity(record,_currentRoom,data,fade));
            // Declared room-local palette owner: original brightenRoom($00)
            // starts from parameter$f0, at speed$01. PART$08 and INTERAC$1e
            // share its admission signal. Full torch initialization has its
            // own scenarios; this tests the terminal palette/door handoff.
            fade.BeginBrighten(0); rom[0xc4ae] = 0xf0;
            SomariaPrivate<FrontendRom>(rom,"_rom").Call(0x3350,0);
            rom.HostilePartsEnabled = true; rom[0xdc00] = 1; rom[0xdc01] = 8;
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceWarpPalette(); rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics();
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    "Room-local brighten/shutter handoff must retain native fixed Link coordinates.");
                for (int y = 0; y < _currentRoom.HeightInTiles; y++)
                for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                {
                    int packed = y*16+x; Vector2 point = new(x*16+8,y*16+8);
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00+packed] ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+packed] ||
                        _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(packed),
                        $"Room-local palette/shutter layout/collision/underlying bytes differ at ${packed:x2}.");
                }
                FailIf(fade.FadeActive != (rom[0xc4ab] != 0) ||
                    SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    _entities.BossEntrySignal != rom[0xcc93] || !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Original brightenRoom/PART/shutter terminal ordering differs on update{++update}.");
            });
            Step(15);
            FailIf(!fade.FadeActive || SomariaPrivate<DoorState>(door,"_state") != DoorState.ReadyToOpen,
                "All 15 nonterminal room-local palette updates must hold opening.");
            Step(1);
            FailIf(fade.FadeActive || SomariaPrivate<int>(door,"_counter") != 6,
                "Room-local fade completion must release the shutter on the terminal 16th update.");
            Step(5); Step(1); Step(3);
        }
    }
}
