using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterPause()
    {
        int fixture = 0;
        foreach (bool text in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            // Isolate the actual source record through its factory boundary,
            // retaining the factory's late-bound text provider. Other room
            // objects/preload are outside this dispatch-gate comparison.
            var factory = SomariaPrivate<RoomEntityFactory>(_entities,"_factory");
            var source = factory.Resources.DungeonMechanics.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e);
            var create = typeof(RoomEntityFactory).GetMethod("CreateDungeonMechanic",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var door = (DungeonDoorRoomEntity)create.Invoke(factory,
                [source,_currentRoom,4,true,default(EnemyPlacementContext),null])!;
            _entities.AddEntity(door);
            Vector2 start = new(184.25f,104.5f); _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start),"Shutter pause fixture must retain unchanged room4:9d floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,184,104);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            door.WriteCounter2Alias(3); rom[0xd247] = 3;
            var freeze = new CrownEntranceFreeze { FreezesRoomEntities = !text }; _entities.AddEntity(freeze);
            if (text) _dialogue.ShowGameplayMessage("Shutter initialization",120);
            rom[0xcba0] = text ? (byte)1 : (byte)0; rom[0xcc8a] = text ? (byte)0 : (byte)2;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batch,afterUpdate:() => {
                FailIf(door.Finished || rom[0xd240] == 0 || door.Counter2Alias != rom[0xd247] ||
                    SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    _entities.BossEntrySignal != rom[0xcc93] || !sounds.Requests.SequenceEqual(rom.Sounds),
                    "Native shutter state0/initialized text and interaction-mask dispatch differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Shutter pause gates must preserve native RNG consumption.");
            });
            Step(3);
            FailIf(SomariaPrivate<DoorState>(door,"_state") != (text ? DoorState.SetRadii : DoorState.SetAngle) ||
                door.Counter2Alias != 0 || rom[0xd244] != 1 || rom[0xd266] != (text ? 0 : 10) ||
                rom[0xd267] != (text ? 0 : 8) || door.UpdatesDuringDialogue || door.UpdatesDuringRoomEntityFreeze,
                "Native state0 must clear inherited counter2 once; text holds radii, while the disable mask permits that first command.");
            _dialogue.Close(); freeze.FreezesRoomEntities = false; rom[0xcba0] = rom[0xcc8a] = 0;
            Step();
            FailIf(SomariaPrivate<DoorState>(door,"_state") != (text ? DoorState.SetAngle : DoorState.InitialBranch),
                "Clearing the mask must resume the saved command without repeating state0.");
            Func<bool> original = _entities.TextActiveSource;
            try
            {
                var pending = SomariaPrivate<DoorState>(door,"_state"); int script = rom.Word(0xd258);
                _entities.TextActiveSource = static () => true; rom[0xcba0] = 1; Step(3);
                FailIf(SomariaPrivate<DoorState>(door,"_state") != pending || rom.Word(0xd258) != script,
                    "A text provider assigned after factory construction must hold the same native script pointer.");
                _entities.TextActiveSource = static () => false; rom[0xcba0] = 0; Step();
                FailIf(SomariaPrivate<DoorState>(door,"_state") == pending || rom.Word(0xd258) == script,
                    "Replacing the provider must resume the pending command on the next eligible update.");
                pending = SomariaPrivate<DoorState>(door,"_state"); script = rom.Word(0xd258);
                _entities.TextActiveSource = static () => true; rom[0xcba0] = 1; Step(2);
                FailIf(SomariaPrivate<DoorState>(door,"_state") != pending || rom.Word(0xd258) != script,
                    "Repeated text must hold the next command without restarting initialization.");
                _entities.TextActiveSource = original; rom[0xcba0] = 0; Step();
                FailIf(SomariaPrivate<DoorState>(door,"_state") == pending || rom.Word(0xd258) == script,
                    "Restoring the gameplay text owner must resume the existing native script.");
            }
            finally { _entities.TextActiveSource = original; }
        }
        fixture = 0;
        foreach (bool enemy in new[] { false,true })
        foreach (bool beforeInitialization in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var source = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e) with { SubId = enemy ? 10 : 6 };
            var door = new DungeonDoorRoomEntity(source,_currentRoom,data,() => 0,_entities.TriggerIsActive,
                p => p-new Vector2(80,48),() => (long)_animationTicks,_sound.PlaySound,
                default,true,_rooms.TrySetTile,_entities.UpdateBossShutterSignal);
            _entities.AddEntity(door); _entities.SetTrigger(0,true);
            Vector2 start = new(184.25f,104.5f); _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start),"Shutter death fixture must retain unchanged room4:9d floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,184,104);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80; rom[0xcca0] = 1;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = (byte)source.SubId; rom[0xd24b] = 0xa7;
            var sounds = _sound.AttachPlayRequestAudit();
            void Hit()
            {
                FailIf(!_player.ApplyDamage(_player.HealthQuarters) || !_player.IsDying,"Lethal damage must publish shutter script death gating.");
                rom.ApplyLinkDamage(unchecked((byte)(-2*rom[0xc6aa])));
                rom[0xd004] = 3; // Resume the dying handler without applying health damage twice.
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceDeathPrelude(); rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics();
                CompareSomariaMotionRom(rom,"Shutter lethal script gate");
                var random = _random.CaptureState();
                FailIf(door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    door.Counter2Alias != rom[0xd247] || _entities.BossEntrySignal != rom[0xcc93] ||
                    _player.IsDying != (rom[0xcdd5] != 0) || _player.HealthQuarters != rom[0xc6aa] ||
                    _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] == 1) ||
                    random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),"Shutter death must retain native Link health, lifetime, counters, shared signal, cues and RNG.");
                if (_player.DeathAnimationActive)
                    FailIf(_player.DeathAnimationCounter != rom[0xd020] || _player.DeathAnimationFrame != rom[0xd031] ||
                        _player.DeathSpinLoopsRemaining != rom[0xd006],"Shutter death animation/counter boundary differs.");
            });
            if (beforeInitialization) Hit();
            else
            {
                for (int wait = 0; rom[0xd244] != 2 && wait < 20; wait++) Step();
                FailIf(rom[0xd244] != 2 || rom[0xd245] != 0,"The original shutter script must select opening before lethal damage.");
                Hit(); Step(); Step(5);
                FailIf(SomariaPrivate<int>(door,"_counter") != 1 || !_currentRoom.IsSolid(door.Position),
                    "Death must retain all six eligible opening updates and the final collision boundary.");
                Step();
                FailIf(_currentRoom.IsSolid(door.Position) || door.Finished ||
                    SomariaPrivate<DoorState>(door,"_state") != (enemy ? DoorState.ScriptEnd : DoorState.WatchingTrigger),
                    "Animation must complete during death while the resumed branch/scriptend stays paused.");
            }
            Step(3);
            FailIf(beforeInitialization && (SomariaPrivate<DoorState>(door,"_state") != DoorState.SetRadii || rom[0xd266] != 0),
                "Death must permit state0 but hold the first script command.");
            Step(32);
            FailIf(door.Finished || sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != (beforeInitialization ? 0 : 2),
                "Actual continuing death must keep the shutter script held after initialization or animation completion.");
        }
    }
}
