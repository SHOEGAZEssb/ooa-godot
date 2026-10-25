using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaRom()
    {
        var animationCounter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (bool batched in new[] { false, true })
        foreach (bool combat in new[] { false, true })
        foreach (int parentSlot in new[] { 0, 5 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf5, 0x80, false);
            LoadValidationRoom(5, 0xf5);
            var placed = _entities.EntityAdapters<TwinrovaRoomEntity>().Single();
            var world = placed.Actor.World;
            _entities.RetireEntity(placed);
            var boss = new TwinrovaRoomEntity(new(world, Vector2.Zero, 0, combat ? 0x80 : 0));
            _entities.RegisterEnemySlot(boss, parentSlot); _entities.AddEntity(boss);
            _player.WarpTo(new(0x78, 0x78)); _player.Face(Vector2I.Up); _transitions.ResetCamera();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x78)
                { HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int at = 0xd080 + parentSlot * 256;
            rom[at] = 1; rom[at + 1] = 3; rom[at + 3] = combat ? (byte)0x80 : (byte)0;
            rom[0xcc08] = 0x33; rom[0xcc0a] = 0x34; rom[0xcc0c] = 0x35; rom[0xcc0e] = 0x36;
            rom[0xcdd1] = 1;
            int cutscene = 0, update = 0, textGeneration = 0, hold = 0;
            _entities.TwinrovaCutscene = value => cutscene = value;
            var sounds = _sound.AttachPlayRequestAudit();
            bool sawShot = false, sawPattern = false;
            StepGameplayUpdates(combat ? 1100 : 340, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); ++update;
                foreach (int slot in Enumerable.Range(0, 16))
                {
                    int address = 0xd080 + slot * 256;
                    var twin = _entities.ResolveEnemySlot(slot) as TwinrovaRoomEntity;
                    FailIf((twin is not null) != (rom[address] != 0 && rom[address + 1] == 3), $"Twinrova update{update}: enemy slot${slot:x2} occupancy differs.");
                    if (twin is null) continue;
                    var a = twin.Actor;
                    FailIf(a.State != rom[address + 4] || a.Substate != rom[address + 5] || a.Subid != rom[address + 2] || a.Var03 != rom[address + 3] ||
                        a.Counter != rom[address + 6] || a.Counter2 != rom[address + 7] || a.Direction != rom[address + 8] ||
                        a.Angle != rom[address + 9] || a.Speed != rom[address + 16] || a.Flags != rom[address + 0x32] ||
                        a.BobCounter != rom[address + 0x31] || a.Pattern != rom[address + 0x33] || a.Waypoint != rom[address + 0x34] ||
                        a.Target != new Vector2I(rom[address + 0x36], rom[address + 0x35]) || a.FacingDelay != rom[address + 0x37] ||
                        a.AttackIndex != rom[address + 0x38] || a.AttackCounter != rom[address + 0x39],
                        $"Twinrova update{update}, slot${slot:x2}, mode={combat}, parentSlot={parentSlot}: native AI state differs; state={a.State:x2}:{a.Substate:x2}/{rom[address+4]:x2}:{rom[address+5]:x2}, count={a.Counter}/{rom[address+6]}, angle={a.Angle}/{rom[address+9]}, direction={a.Direction}/{rom[address+8]}, flags={a.Flags:x2}/{rom[address+0x32]:x2}, bob={a.BobCounter}/{rom[address+0x31]}, waypoint={a.Waypoint}/{rom[address+0x34]}.");
                    FailIf(a.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        a.ZFixed != unchecked((short)rom.Word(address + 14)) || a.Visible != ((rom[address + 0x1a] & 128) != 0) ||
                        (int)animationCounter.GetValue(a.Animation)! != rom[address + 0x20] || a.AnimationParameter != rom[address + 0x21] || a.Health != rom[address + 0x29],
                        $"Twinrova update{update}, slot${slot:x2}: position/Z/visibility/animation/HP differs; YX={a.Position}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, Z={a.ZFixed}/{unchecked((short)rom.Word(address+14))}, visible={a.Visible}/{rom[address+0x1a]:x2}, anim={animationCounter.GetValue(a.Animation)}/{rom[address+0x20]} param={a.AnimationParameter}/{rom[address+0x21]}.");
                    sawPattern |= a.Pattern != 0;
                }
                foreach (int slot in Enumerable.Range(0, 16))
                {
                    int address = 0xd0c0 + slot * 256;
                    var shot = _entities.ResolvePartSlot(slot) as TwinrovaProjectileRoomEntity;
                    FailIf((shot is not null) != (rom[address] != 0 && rom[address + 1] is 0x4b or 0x4d), $"Twinrova update{update}: PART slot${slot:x2} occupancy differs.");
                    if (shot is null) continue;
                    sawShot = true;
                    FailIf(shot.State != rom[address + 4] || shot.Counter != rom[address + 6] || shot.Angle != rom[address + 9] || shot.Speed != rom[address + 16] ||
                        shot.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) || shot.ZFixed != unchecked((short)rom.Word(address + 14)) ||
                        (int)animationCounter.GetValue(shot.Animation)! != rom[address + 0x20],
                        $"Twinrova update{update}: PART${shot.Id:x2} state={shot.State}/{rom[address+4]}, counter={shot.Counter}/{rom[address+6]}, angle={shot.Angle}/{rom[address+9]}, speed={shot.Speed}/{rom[address+16]}, pos={shot.Position}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, z={shot.ZFixed}/{unchecked((short)rom.Word(address+14))}, anim={animationCounter.GetValue(shot.Animation)}/{rom[address+0x20]}.");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) || !sounds.Requests.SequenceEqual(rom.Sounds) ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != rom[0xcc8a] || _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != rom[0xcc02],
                    $"Twinrova update{update}: native RNG/audio/masks differ; RNG={_random.Calls-seed.Calls}/{rom.RandomCalls}, audio={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if (textGeneration != rom.TextGeneration)
                {
                    textGeneration = rom.TextGeneration; hold = 3;
                    FailIf(!_dialogue.IsOpen || rom.Word(0xcba2) is not (0x3304 or 0x3305), $"Twinrova update{update}: opening text differs, open={_dialogue.IsOpen}, source={_dialogue.SourceTextId}, native={rom.Word(0xcba2):x4}, message={_dialogue.CurrentMessage}.");
                }
                if (hold != 0 && --hold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
                _player.ApplyInteractionInvincibility(128); rom[0xd02b] = 0x80;
            });
            _sound.SetRequestObserver(null);
            FailIf(combat && (!sawShot || !sawPattern) || !combat && cutscene != 0x18,
                $"Twinrova bounded regression missed attacks/movement or opening handoff: combat={combat}, shots={sawShot}, pattern={sawPattern}, cutscene={cutscene:x2}.");
        }
        GD.Print("Validated paired Twinrova initialization, lower-slot role swap, source opening and combat movement/projectile charge in split/batched gameplay against the clean ROM.");
    }
}
