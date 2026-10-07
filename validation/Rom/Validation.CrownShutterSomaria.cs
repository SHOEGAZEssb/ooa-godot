using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterSomaria()
    {
        int fixture = 0;
        foreach (bool somaria in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria,1); _inventory.EquipA(TreasureId.CaneOfSomaria); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var source = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e);
            var door = new DungeonDoorRoomEntity(source,_currentRoom,data,() => 0,_entities.TriggerIsActive,
                p => p-new Vector2(80,48),() => (long)_animationTicks,_sound.PlaySound,
                default,true,_rooms.TrySetTile,_entities.UpdateBossShutterSignal);
            _entities.AddEntity(door); _entities.SetTrigger(0,true);
            bool placementObserved = false,nativePlacementObserved = false;
            var observer = new ItemPhaseValidationEntity(() => {
                if (SomariaPrivate<DoorState>(door,"_state") != DoorState.ReadyToClose ||
                    !_entities.EntityAdapters<SomariaBlockRoomEntity>().Any(entity => entity.Block.State == 3 && !entity.Finished)) return;
                FailIf(_currentRoom.GetMetatile(door.Position) != 0xda || !_currentRoom.IsSolid(door.Position),
                    "Actual ITEM pass must place Somaria before the selected shutter's INTERACTION pass.");
                placementObserved = true;
            });
            _entities.AddEntity(observer); _entities.RegisterEnemySlot(observer,0);
            Vector2 start = new(120.25f,136.5f); _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start),"Cane/shutter approach must begin on unchanged room4:9d floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,120,136);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80; rom[0xcca0] = 1;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,bool press = false)
            {
                int edge = press ? 1 : 0;
                StepGameplayUpdates(count,angle == 16 ? Vector2.Down : Vector2.Zero,press ? ["attack"] : [],press ? ["attack"] : [],batch,() => {
                    rom.UpdateGameplay(edge,(press ? 1 : 0)|(angle == 16 ? 0x80 : 0),angle,_entities.FrameCounter,() => {
                        if (rom[0xd244] != 3 || rom[0xd245] != 0 || rom.Blocks.Length != 1 || rom[rom.Blocks[0]+4] != 3) return;
                        FailIf(rom[0xcfa7] != 0xda || rom[0xcea7] != 0x0f,"Native ITEM pass must place Somaria before shutter closing.");
                        nativePlacementObserved = true;
                    });
                    edge = 0;
                    CompareSomariaMotionRom(rom,"Actual Cane/shutter handoff");
                    rom.AdvanceTileGraphics();
                    string context = $"Cane/shutter somaria={somaria}, batch={batch}, update={++update}";
                    var random = _random.CaptureState();
                    FailIf(door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                        _entities.BossEntrySignal != rom[0xcc93] || _entities.ActiveTriggers != rom[0xcca0] ||
                        random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds),context+": original door/Cane lifetime/counters/shared signals/cues/RNG differ.");
                });
            }
            Step(20);
            FailIf(_currentRoom.IsSolid(door.Position),"Active source trigger must open the shutter before actual Cane placement.");
            Step(13,16);
            FailIf(_player.PrecisePosition != start+new Vector2(0,13) || _collision.Collides(_player.Position),
                "Cane must approach the open shutter through its actual adjacent floor.");
            for (int repeat = 0; repeat < (somaria ? 2 : 1); repeat++)
            {
                if (repeat != 0)
                {
                    _entities.SetTrigger(0,true); rom[0xcca0] = 1; Step(20);
                    FailIf(_currentRoom.IsSolid(door.Position),"Repeated Cane use requires the same source shutter to reopen.");
                }
                if (somaria)
                {
                    placementObserved = nativePlacementObserved = false;
                    Step(press:true);
                    for (int wait = 0; !(rom.Blocks.Length == 1 && rom[rom.Blocks[0]+4] == 1) && wait < 40; wait++) Step();
                    FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0]+4] != 1,"Real Cane creation must allocate a phasing ITEM$18.");
                    // Original phase-in is three frames of three updates.
                    // Release the trigger with three updates remaining: the
                    // script selects closing on the next two updates, then
                    // ITEM$18 places its solid tile before state3 runs.
                    Step(6);
                    FailIf(rom[rom.Blocks[0]+0x20] != 3 || _currentRoom.IsSolid(door.Position),
                        "Cane phase-in must retain the open shutter for the final three-update handoff.");
                }
                _entities.SetTrigger(0,false); rom[0xcca0] = 0; Step(2);
                FailIf(SomariaPrivate<DoorState>(door,"_state") != DoorState.ReadyToClose || rom[0xd244] != 3,
                    "Inactive trigger must select native closing before the next INTERACTION update.");
                if (!somaria)
                {
                    // Declared competing tile-owner write in this handoff gap.
                    // Both source/runtime canonical setters supply collision.
                    FailIf(!_rooms.TrySetTile(0xa7,0x7b),"Competing solid tile write must fit the shared queue.");
                    rom.SetTile(0xa7,0x7b); Step(); Step(3);
                    FailIf(_currentRoom.GetMetatile(door.Position) != 0x7b || !_currentRoom.IsSolid(door.Position) ||
                        sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 2,
                        "Closing must skip an unrelated solid tile without changing its bytes or playing a closing cue.");
                    continue;
                }
                Step();
                FailIf(SomariaPrivate<DoorState>(door,"_state") != DoorState.ClosingInterleaved ||
                    SomariaPrivate<int>(door,"_counter") != 6 || _currentRoom.GetMetatile(door.Position) != 0xa0 ||
                    !_currentRoom.IsSolid(door.Position) || rom.Blocks.Length != 1 || !placementObserved || !nativePlacementObserved ||
                    rom[rom.Blocks[0]+0x32] != 0xa7,
                    "Closing must replace Somaria layout immediately while the earlier ITEM pass retains the live block for this update.");
                Step();
                FailIf(rom.Blocks.Length != 0 || _entities.EntityAdapters<SomariaBlockRoomEntity>().Any(entity => !entity.Finished),
                    "The next ITEM update must delete the displaced block without restoring its stale underlying floor.");
                Step(4);
                FailIf(SomariaPrivate<int>(door,"_counter") != 1 || _currentRoom.GetMetatile(door.Position) != 0xa0,
                    "Somaria displacement must retain the shutter's final six-update boundary.");
                Step(); Step(20);
                FailIf(_currentRoom.GetMetatile(door.Position) != 0x7a || !_currentRoom.IsSolid(door.Position) || rom.Blocks.Length != 0,
                    "Shutter$7a must survive completion and the displaced block/puff lifetime before repeat use.");
            }
        }
    }
}
