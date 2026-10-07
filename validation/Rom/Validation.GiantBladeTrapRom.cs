using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGiantBladeTrapRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0x28);
            var traps = _entities.Entities<GiantBladeTrapCharacter>().ToArray();
            FailIf(traps.Length != 2 || traps[0].Position != new Vector2(0x40, 0x20) ||
                traps[1].Position != new Vector2(0xb0, 0x90) || _entities.RoomEnemyCount != 3,
                "Mermaid room $5:$28 must retain three counted Keese followed by two uncounted $2a:$03 traps.");
            // Retire unrelated Keese before either dispatcher initializes
            // them. The trace uses the room's unchanged collision geometry.
            foreach (var keese in _entities.Entities<KeeseCharacter>()) keese.FinishGale();
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                .GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var enemies = slots.Where(pair => pair.Key.Node is GiantBladeTrapCharacter).ToArray();
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x2a * 4];
            rom[0xcdd1] = 0;
            var seed = _random.CaptureState();
            rom[0xff94] = seed.Rng1;
            rom[0xff95] = seed.Rng2;
            foreach (var (entity, slot) in enemies)
            {
                int address = 0xd080 + slot * 0x100;
                rom[address] = 3;
                rom[address + 1] = 0x2a;
                rom[address + 2] = 3;
                rom.Word(address + 10, (int)(entity.Node.Position.Y * 256));
                rom.Word(address + 12, (int)(entity.Node.Position.X * 256));
            }
            bool frozen = true;
            int update = 0, turns = 0;
            int[] angles = new int[2];
            _entities.TextActiveSource = () => frozen;
            void Compare()
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                for (int index = 0; index < enemies.Length; index++)
                {
                    var enemy = (GiantBladeTrapCharacter)enemies[index].Key.Node;
                    int address = 0xd080 + enemies[index].Value * 0x100;
                    FailIf(enemy.State != rom[address + 4] || enemy.Counter != rom[address + 6] ||
                        enemy.Angle != rom[address + 9] || enemy.Speed != rom[address + 16] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        enemy.Health != rom[address + 0x29] ||
                        (enemy.InvincibilityCounter & 0xff) != rom[address + 0x2b] ||
                        enemy.Visible != ((rom[address + 0x1a] & 0x80) != 0),
                        $"Giant Blade Trap $5:$28 slot${enemies[index].Value:x2}, update={update}, batch={batched}: " +
                        $"runtime state/counter/angle/speed=${enemy.State:x2}/{enemy.Counter}/${enemy.Angle:x2}/${enemy.Speed:x2}, XY={enemy.Position}, inv={enemy.InvincibilityCounter}; " +
                        $"ROM=${rom[address + 4]:x2}/{rom[address + 6]}/${rom[address + 9]:x2}/${rom[address + 16]:x2}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, inv=${rom[address + 0x2b]:x2}.");
                    if (enemy.State == 9 && angles[index] != enemy.Angle) turns++;
                    angles[index] = enemy.Angle;
                }
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] ||
                    actual.Calls - seed.Calls != rom.RandomCalls,
                    $"Giant Blade Trap $5:$28 update={update}: common RNG consumption differs from native dispatch.");
                update++;
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(traps.Any(trap => trap.State != 8), "$2a:$03 state zero must initialize under text and freeze before state8.");
            frozen = false;
            StepGameplayUpdates(220, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(turns < 4, "$5:$28 trap trace must exercise wall waits and multiple source turns.");
            frozen = true;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            // ENEMYDMG_34 supplies negative invincibility and JUST_HIT.
            // Unlike blue traps, giant trap AI continues on this update.
            foreach (var (entity, slot) in enemies)
            {
                var trap = (GiantBladeTrapCharacter)entity.Node;
                trap.InvincibilityCounter = -28;
                trap.DeferNativeHitStatus();
                rom[0xd0ab + slot * 0x100] = 0xe4;
                rom[0xd0aa + slot * 0x100] = 0x80;
            }
            StepGameplayUpdates(30, Vector2.Zero, batched: batched, afterUpdate: Compare);

            _entities.Clear();
            _entities.BeginScreenTransition(5, _currentRoom, Vector2.Left * _currentRoom.Width, _player);
            var incoming = _entities.Entities<GiantBladeTrapCharacter>().ToArray();
            FailIf(incoming.Length != 2 || incoming.Any(trap => !trap.Visible || trap.State != 8),
                "$5:$28 incoming giant traps must complete state zero while preloaded.");
            var positions = incoming.Select(trap => trap.Position).ToArray();
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(incoming.Where((trap, index) => trap.State != 8 || trap.Position != positions[index]).Any(),
                "$5:$28 incoming giant trap AI must freeze during scrolling.");
            _entities.FinishScreenTransition();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(incoming.Any(trap => trap.State != 9 || trap.Angle != 0x10 || trap.Counter != 90),
                "$5:$28 incoming giant traps must begin their source acceleration after scrolling.");
        }
        GD.Print("Validated both $5:$28 Giant Blade Traps against native updates: acceleration, paired raw wall probes, fractional wall snaps, turn waits, JUST_HIT continuation, RNG, text freeze and scroll preload through split/batched gameplay.");
    }
}
