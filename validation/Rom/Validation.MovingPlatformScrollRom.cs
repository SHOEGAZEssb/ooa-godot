using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMovingPlatformScrollRom()
    {
        int fixture = 0;
        foreach (bool initialized in new[] { false, true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x16); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(103.25f,152.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start), "Platform script scroll must approach the original room$4:$16 bottom exit.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,103,152);
            rom.Word(0xd00c,(int)(start.X*256)); rom.Word(0xd00a,(int)(start.Y*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 1;
            var mechanics = new DungeonMechanicDatabase();
            var doorRecord = mechanics.GetRoomRecords(4,0x16).Single(row=>row.Id == 0x1e);
            var door = new DungeonDoorRoomEntity(doorRecord,_currentRoom,mechanics,()=>0,_entities.TriggerIsActive,
                point=>point,()=>(long)_animationTicks,_sound.PlaySound,default,true,_rooms.TrySetTile,
                isOutgoing:_entities.IsOutgoingEntity);
            _entities.AddEntity(door);
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            // Declare the left-button signal while its PART producer is
            // excluded; let the original shutter script open the exit.
            _entities.SetTrigger(0,true); rom[0xcca0] = 1;
            for (int wait = 0; _currentRoom.IsSolid(door.Position) && wait < 60; wait++)
                StepSomariaMotionRom(rom,1,batch,afterUpdate:rom.AdvanceTileGraphics);
            FailIf(_currentRoom.GetMetatile(door.Position) != 0xa0 || rom[0xcfA7] != 0xa0,
                $"Original south shutter must write its exit floor before Link's approach: tile=${_currentRoom.GetMetatile(door.Position):x2}/${rom[0xcfA7]:x2}, state={SomariaPrivate<DoorState>(door,"_state")}/${rom[0xd244]:x2}, trigger=${_entities.ActiveTriggers:x2}/${rom[0xcca0]:x2}.");
            StepSomariaMotionRom(rom,20,batch,afterUpdate:rom.AdvanceTileGraphics);
            StepSomariaMotionRom(rom,17,batch,8);
            StepSomariaMotionRom(rom,9,batch,16);
            FailIf(_player.PrecisePosition != start+new Vector2(17,9) || _collision.Collides(_player.Position),
                "Platform script exit approach must traverse the original floor and opened shutter.");
            // Declare the distant original $20:$05 producer after the last
            // approach object pass. Its trigger producer is excluded here;
            // actual button pressure is covered by the spawner comparison.
            var outgoing = new SpiritsGraveMovingPlatformSpawner(_entities.TriggerIsActive,_sound.PlaySound,30,
                _runtimeState,_entities.IsScriptTextActive,_entities.TryCreatePuzzlePuff,_entities.TryCreateMovingPlatform);
            _entities.AddEntity(outgoing);
            int address = (0xd0+_entities.InteractionSlot(outgoing))*256+0x40;
            rom[address] = 1; rom[address+1] = 0x20; rom[address+2] = 5;
            if (initialized)
            {
                _entities.SetTrigger(1,true); rom[0xcca0] = 3;
                StepSomariaMotionRom(rom,3,batch);
                FailIf(SomariaPrivate<int>(outgoing,"_counter") != 30 || rom[address+6] != 30,
                    "Original script must reach wait30 through both separately yielded puff allocations.");
            }
            int pointer = rom.Word(address+0x18);
            int phase = SomariaPrivate<int>(outgoing,"_state");
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Down,out int target) || target != 0x1a,
                "Platform script scroll must use original dungeon adjacency$4:$16 -> $4:$1a.");
            _transitions.BeginScroll(_player,Vector2I.Down,target);
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(true,2,(int)(_player.PrecisePosition.X*256),(int)(_player.PrecisePosition.Y*256),unique,sourceUnique);
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(scrolling:true); rom[0xcd00] = 8;
            rom[0xcc30] = 0x1a; rom.CopyRoom(_currentRoom);
            // mainData.s $4:$1a has only an enemy pointer. Incoming enemy
            // parsing/dispatch, placement RNG and pixels are excluded; the
            // outgoing script, physical puffs and scroll execute native code.
            var sounds = _sound.AttachPlayRequestAudit();
            int priorSounds = rom.Sounds.Count;
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
                else rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter,()=>rom[0xcc96] = rom[0xcc8d]);
                bool alive = rom[address] != 0 && rom[address+1] == 0x20;
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2) ||
                    _entities.OutgoingEntities<SpiritsGraveMovingPlatformSpawner>().Count != (alive ? 1 : 0) ||
                    alive && (outgoing.Finished || SomariaPrivate<int>(outgoing,"_counter") != rom[address+6] ||
                        SomariaPrivate<int>(outgoing,"_state") != phase || rom.Word(address+0x18) != pointer),
                    $"Platform script scroll initialized={initialized}, batch={batch}, frame={_entities.FrameCounter}: outgoing={outgoing.Finished}/${rom[address]:x2}, counter={SomariaPrivate<int>(outgoing,"_counter")}/{rom[address+6]}.");
                Vector2 point = new(((int)(_player.PrecisePosition.X*256)&0xffff)/256f,((int)(_player.PrecisePosition.Y*256)&0xffff)/256f);
                FailIf(point != (frozen ? new Vector2(scroll.Word(0xd00c)/256f,scroll.Word(0xd00a)/256f) :
                    new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)),
                    "Platform script scroll must match native precise Link coordinates during and after completion.");
                var puffs = _entities.OutgoingEntities<PuzzlePuffEffect>();
                int[] nativePuffs = Enumerable.Range(0xd2,14).Select(page=>page*256+0x40)
                    .Where(address=>rom[address] != 0 && rom[address+1] == 5).ToArray();
                FailIf(puffs.Count != nativePuffs.Length,"Outgoing platform puff lifetimes must match native scroll eligibility.");
                foreach (var puff in puffs)
                {
                    int address = (0xd0+_entities.InteractionSlot(puff))*256+0x40;
                    FailIf(!nativePuffs.Contains(address) || puff.Initialized != (rom[address+4] != 0) ||
                        puff.CurrentParameter != rom[address+0x21] || SomariaPrivate<int>(puff,"_animationCounter") != rom[address+0x20],
                        "Outgoing platform puffs must retain their physical-slot animation during scrolling.");
                }
                FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != 0 ||
                    !sounds.Requests.SequenceEqual(rom.Sounds.Skip(priorSounds)),
                    "Scrolling must not resume the platform spawn or publish a delayed solve cue.");
            });
            int total = _transitions.ScrollTotalFrames; Step();
            FailIf(outgoing.Finished == initialized || initialized && SomariaPrivate<int>(outgoing,"_counter") != 30,
                "First native scroll pass must delete pending $20:$05 and freeze its initialized wait30.");
            Step(total-1); Step(3);
            FailIf(_transitions.ScrollActive || rom[address] != 0 ||
                _entities.OutgoingEntities<SpiritsGraveMovingPlatformSpawner>().Count != 0,
                "Scroll completion must bulk-clear the retained platform producer without resuming it.");
        }
    }
}
