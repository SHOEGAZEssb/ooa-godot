using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterScroll()
    {
        int fixture = 0;
        foreach (bool initialized in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xa5); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(220.25f,88.5f); _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(new(232,88)) != 0xa0,
                "Shutter scroll must approach room4:a5's unchanged right exit floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,220,88);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batch,8);
            FailIf(_player.PrecisePosition != start+new Vector2(9,0) || _collision.Collides(_player.Position),
                "Original right-exit approach must retain nine collision-free walking updates.");
            var data = new DungeonMechanicDatabase();
            // Declare a distant common shutter after the last object pass.
            // Its allocating producer is outside this outgoing-owner fixture;
            // pending/initialized state are the inputs at the scroll boundary.
            var source = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e);
            var outgoing = new DungeonDoorRoomEntity(source,_currentRoom,data,() => 0,_entities.TriggerIsActive,
                p => p,() => (long)_animationTicks,_sound.PlaySound,default,true,_rooms.TrySetTile,
                _entities.UpdateBossShutterSignal,isOutgoing:_entities.IsOutgoingEntity);
            _entities.AddEntity(outgoing);
            FailIf(_entities.InteractionSlot(outgoing) != 2,"Declared outgoing shutter must occupy the original first dynamic slot$d2.");
            rom.CopyRoom(_currentRoom);
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            if (initialized) StepSomariaMotionRom(rom,1,batch);
            var oldState = SomariaPrivate<DoorState>(outgoing,"_state"); int counter = SomariaPrivate<int>(outgoing,"_counter");
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Right,out int target) || target != 0xa6,
                "Shutter entrance must use imported dungeon adjacency4:a5 ->4:a6.");
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            _transitions.BeginScroll(_player,Vector2I.Right,target);
            var incoming = _entities.Entities<DungeonDoorRoomEntity>().Single();
            FailIf(incoming.SubId != 7 || incoming.PackedPosition != 0x50 || !incoming.EnteredThroughThisDoor ||
                _entities.InteractionSlot(incoming) != 4 || SomariaPrivate<DoorState>(incoming,"_state") != DoorState.Initialize,
                "Original incoming order must retain translator$d3, shutter$d4 and scanner$d5 after the held outgoing$d2.");
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(true,1,(int)(_player.PrecisePosition.X*256),(int)(_player.PrecisePosition.Y*256),unique,sourceUnique);
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(scrolling:true); rom[0xcd00] = 8;
            rom[0xcc30] = 0xa6; rom.CopyRoom(_currentRoom);
            // Independent mainData.s / extraData1.s interaction prefix. This
            // comparison isolates object dispatch/cleanup and scroll control;
            // complete incoming enemy/PART parsing, RNG and pixels are excluded.
            rom[0xd340] = 1; rom[0xd341] = 0x24; rom[0xd342] = 2; rom[0xd34b] = 4; rom[0xd34d] = 1;
            rom[0xd440] = 1; rom[0xd441] = 0x1e; rom[0xd442] = 7; rom[0xd44b] = 0x50;
            rom[0xd540] = 1; rom[0xd541] = 0xc7; rom[0xd542] = 8; rom[0xd54b] = 6; rom[0xd54d] = 0x10;
            var sounds = _sound.AttachPlayRequestAudit();
            PuzzlePuffEffect? puff = null; int puffSlot = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool frozen = scroll[0xcd04] != 2;
                if (frozen)
                {
                    rom.AdvanceInteractions(_entities.FrameCounter); scroll.Update();
                    if (scroll[0xcd04] == 2)
                    {
                        rom.ClearOutgoingInteractions(); rom[0xcd00] = 1;
                        rom.Word(0xd00c,scroll.Word(0xd00c)); rom.Word(0xd00a,scroll.Word(0xd00a));
                    }
                }
                else
                {
                    rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                        "Post-scroll Link must resume at independently executed native scroll coordinates.");
                }
                bool sourceAlive = rom[0xd240] != 0 && rom[0xd241] == 0x1e;
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2) || scroll[0xcd04] != 2 && outgoing.Finished == sourceAlive ||
                    sourceAlive && SomariaPrivate<int>(outgoing,"_counter") != rom[0xd246] ||
                    _entities.OutgoingEntities<DungeonDoorRoomEntity>().Count != (sourceAlive ? 1 : 0),
                    $"Native shutter scroll initialized={initialized}, batch={batch}, frame={_entities.FrameCounter}: scroll={_transitions.ScrollActive}/{scroll[0xcd04]}, outgoing={outgoing.Finished}/${rom[0xd240]:x2}, counter={SomariaPrivate<int>(outgoing,"_counter")}/{rom[0xd246]}, outgoingcount={_entities.OutgoingEntities<DungeonDoorRoomEntity>().Count}.");
                if (frozen)
                {
                    Vector2 point = new(((int)(_player.PrecisePosition.X*256)&0xffff)/256f,((int)(_player.PrecisePosition.Y*256)&0xffff)/256f);
                    FailIf(point != new Vector2(scroll.Word(0xd00c)/256f,scroll.Word(0xd00a)/256f) ||
                        SomariaPrivate<DoorState>(incoming,"_state") != DoorState.Initialize || rom[0xd444] != 0 ||
                        _currentRoom.GetMetatile(incoming.Position) != 0xa0 || _currentRoom.IsSolid(incoming.Position) ||
                        initialized && sourceAlive && SomariaPrivate<DoorState>(outgoing,"_state") != oldState,
                        "Incoming shutter must retain its source entry-floor substitution and pending script throughout native scrolling.");
                }
                if (puff != null)
                {
                    bool alive = rom[puffSlot] != 0;
                    FailIf(puff.Finished == alive,"Slot-reuse puff lifetime must match its native interaction.");
                    if (alive) FailIf(puff.Initialized != (rom[puffSlot+4] != 0) ||
                        puff.CurrentParameter != rom[puffSlot+0x21] || SomariaPrivate<int>(puff,"_animationCounter") != rom[puffSlot+0x20],
                        "Reused-slot puff must retain native initialization, animation parameter and counter during scrolling.");
                }
                bool DoorCue(int cue) => cue is SoundId.SndDoorClose or SoundId.SndSolvePuzzle;
                FailIf(sounds.Requests.Where(DoorCue).Any() || rom.Sounds.Where(DoorCue).Any(),
                    "Frozen and newly arriving shutters must not emit delayed opening/closing/solve cues.");
            });
            int total = _transitions.ScrollTotalFrames; Step();
            FailIf(outgoing.Finished == initialized || initialized && SomariaPrivate<int>(outgoing,"_counter") != counter,
                "First native scroll pass must delete outgoing state0 and hold an initialized shutter.");
            puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
            puffSlot = 0xd000+_entities.InteractionSlot(puff)*256+0x40;
            var native = SomariaPrivate<FrontendRom>(rom,"_rom");
            int linkX = rom.Word(0xd00c),linkY = rom.Word(0xd00a);
            rom.Word(0xd00c,24*256); rom.Word(0xd00a,24*256); rom[0xffae] = 0;
            byte[] caller = [0x16,0xd0,0xcd,0xc1,0x24,0xc9]; // Original objectCreatePuff/getFreeInteractionSlot.
            for (int index = 0; index < caller.Length; index++) rom[0xc100+index] = caller[index];
            native.Call(0xc100,0); rom.Word(0xd00c,linkX); rom.Word(0xd00a,linkY);
            FailIf(puffSlot != (initialized ? 0xd540 : 0xd240) || rom[puffSlot] == 0 || rom[puffSlot+1] != 5,
                "Original allocation must reuse deleted$d2 or scanner$d5 while the initialized outgoing shutter retains$d2.");
            Step(total-1);
            FailIf(_transitions.ScrollActive || _entities.OutgoingEntities<DungeonDoorRoomEntity>().Count != 0 || rom[0xd240] != 0,
                "Native scroll completion must bulk-clear the initialized outgoing shutter.");
            Step();
            FailIf(SomariaPrivate<DoorState>(incoming,"_state") != DoorState.SetAngle || rom[0xd444] != 1 ||
                rom[0xd466] != 8 || rom[0xd467] != 10 || _entities.BossEntrySignal != rom[0xcc93],
                "First arrival update must initialize the source left shutter and its horizontal radii, without early scroll dispatch.");
            Step(3);
        }
    }
}
