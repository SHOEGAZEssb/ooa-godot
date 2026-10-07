using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterSwitchHook()
    {
        int fixture = 0;
        foreach (bool opening in new[] { true,false })
        foreach (bool cancel in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x7c); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook,1); _inventory.EquipA(TreasureId.SwitchHook); _inventory.EquipB(0);
            Vector2 start = new(184.25f,88.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(new(136,88)) != 0xdb,
                "Shutter hook handoff must use unchanged room4:7c floor and its original diamond.");
            var data = new DungeonMechanicDatabase();
            // Declare the shared trigger shutter at the distant bottom wall.
            // This isolates ITEM/INTERACTION ordering from room placement.
            var source = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e);
            var door = new DungeonDoorRoomEntity(source,_currentRoom,data,() => 0,
                _entities.TriggerIsActive,p => p,() => (long)_animationTicks,_sound.PlaySound,
                default,true,_rooms.TrySetTile,_entities.UpdateBossShutterSignal);
            _entities.AddEntity(door); _entities.SetTrigger(0,!opening);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,184,88);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcca0] = opening ? (byte)0 : (byte)1;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool press = false) =>
                StepSomariaMotionRom(rom,count,batch,held:press ? 1 : 0,pressed:press ? 1 : 0,afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    var hook = _entities.SwitchHook!.Item;
                    bool native = rom[0xd600] != 0 && rom[0xd601] == 0x0a;
                    string context = $"Shutter Hook opening={opening}, cancel={cancel}, batch={batch}, update={++update}";
                    FailIf((hook is { Finished:false }) != native || _player.IsUsingSwitchHook != (rom[0xd200] != 0) ||
                        _entities.SwitchHook.ExchangeState != rom[0xccdd] || (_player.SwitchHookZFixed&0xffff) != rom.Word(0xd00e) ||
                        door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                        _entities.BossEntrySignal != rom[0xcc93] || _entities.ActiveTriggers != rom[0xcca0],
                        context+": original parent/weapon/exchange/Link Z/shutter counter/shared signals differ.");
                    if (native)
                        FailIf(hook!.State != rom[0xd604] || hook.Substate != rom[0xd605] || hook.Counter != rom[0xd606] ||
                            hook.ZHigh != (sbyte)rom[0xd60f] || hook.PrecisePosition != new Vector2(rom.Word(0xd60c)/256f,rom.Word(0xd60a)/256f),
                            context+": live Hook state/counter/fixed XY/Z differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                        context+": ordered Hook/solve/door cues or RNG differ.");
                });
            Step(20); Step(press:true);
            for (int wait = 0; _entities.SwitchHook!.ExchangeState != 1 && wait < 50; wait++) Step();
            FailIf(_entities.SwitchHook!.ExchangeState != 1,"Actual diamond contact must enter native latch state$01.");
            Step(opening ? 13 : 14);
            _entities.SetTrigger(0,opening); rom[0xcca0] = opening ? (byte)1 : (byte)0;
            Step(opening ? 4 : 3);
            DoorState interleave = opening ? DoorState.OpeningInterleaved : DoorState.ClosingInterleaved;
            FailIf(_entities.SwitchHook.ExchangeState != 1 || SomariaPrivate<DoorState>(door,"_state") != interleave ||
                SomariaPrivate<int>(door,"_counter") != 6,"Latch$01 must permit the shutter to begin its exact six-update interleave.");
            Step();
            FailIf(_entities.SwitchHook.ExchangeState != 2 || SomariaPrivate<int>(door,"_counter") != 6,
                "ITEM must publish exchange$02 before the same update's INTERACTION pass.");
            Step(cancel ? 5 : 32);
            FailIf(_entities.SwitchHook.ExchangeState != 2 || SomariaPrivate<int>(door,"_counter") != 6,
                "Lift/swap/lowering must freeze the shutter's counter.");
            if (cancel)
            {
                // Declared parent deletion: original itemCode0aPost detects
                // the missing parent after interactions and clears ccdd.
                // This is not a damage or room-reload eligibility claim.
                _entities.SwitchHook.ClearParent(); rom.ClearItemParents(); Step();
                FailIf(_entities.SwitchHook.ExchangeState != 0 || SomariaPrivate<int>(door,"_counter") != 6,
                    "Post-pass parent-loss cancellation must clear exchange after the shutter's frozen update.");
            }
            Step();
            FailIf(_entities.SwitchHook.ExchangeState != 0 || SomariaPrivate<int>(door,"_counter") != 5,
                "Completion/cancellation must release the shutter on the first update sampling exchange$00.");
            Step(4);
            FailIf(SomariaPrivate<int>(door,"_counter") != 1 || SomariaPrivate<DoorState>(door,"_state") != interleave,
                "Resumed shutter must retain the final collision boundary.");
            Step(); Step(3);
            FailIf(_currentRoom.IsSolid(door.Position) == opening || SomariaPrivate<DoorState>(door,"_state") != DoorState.WatchingTrigger,
                "Shutter must complete after six eligible updates and remain stable after exchange.");
        }
    }
}
