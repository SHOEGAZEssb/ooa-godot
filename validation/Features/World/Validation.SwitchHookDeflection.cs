using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookDeflection()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var pending = typeof(SwitchHookItem).GetField("_objectCollisionPending", flags)!;
        void Step(int count = 1, bool press = false)
        {
            input.CaptureForValidation(press ? ["attack"] : [], press ? ["attack"] : [], Vector2.Zero);
            scheduler.Advance(count / 60.0, update);
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        var random = CaptureOracleRandomForValidation();

        bool isolateTarget = true;
        bool Approach(EnemyCharacter enemy)
        {
            foreach (Vector2I direction in new[] { Vector2I.Down, Vector2I.Up, Vector2I.Left, Vector2I.Right })
            {
                Vector2 origin = enemy.Position.Floor() + (Vector2)direction * 24;
                if (origin.X < 12 || origin.X >= _currentRoom.Width - 12 ||
                    origin.Y < 12 || origin.Y >= _currentRoom.Height - 12) continue;
                Vector2 sideways = new(-direction.Y, direction.X);
                if (!Enumerable.Range(0, 25).All(i => Enumerable.Range(-5, 11).All(j =>
                    {
                        Vector2 point = enemy.Position.Floor() + (Vector2)direction * i + sideways * j;
                        return !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None;
                    }))) continue;
                if (_entities.Entities<EnemyCharacter>().Any(other => other != enemy &&
                    (isolateTarget ? other.Position.DistanceTo((origin + enemy.Position) / 2) < 20
                        : other.Position.DistanceTo(origin) < 12))) continue;
                _player.WarpTo(origin);
                _player.Face(-direction);
                return true;
            }
            return false;
        }

        foreach (var (room, id, mode, effect) in new[] {
            (0x70, 0x0e, 0x13, 0x1b), (0x7f, 0x13, 0x17, 0x1c),
            (0x88, 0x19, 0x1c, 0x1c), (0x7d, 0x4d, 0x38, 0x0d) })
        {
            FailIf(!SwitchHookCollisionDatabase.Shared.EnemyEnabled(id) ||
                SwitchHookCollisionDatabase.Shared.Effect(mode) != effect,
                $"Enemy ${id:x2} lost its source hook mask/effect ${effect:x2}.");

            (Vector2 Enemy, int Invincibility, int Recoil, int State) Run(bool batch, bool repeat)
            {
                RestoreOracleRandomForValidation(random);
                LoadValidationRoom(4, room);
                isolateTarget = true;
                var enemies = _entities.Entities<EnemyCharacter>().Where(e => id switch {
                    0x0e => e is BladeTrapCharacter, 0x13 => e is SparkCharacter,
                    0x19 => e is WhispCharacter, _ => e is HardhatBeetleCharacter }).ToArray();
                var enemy = enemies.FirstOrDefault(Approach);
                for (int i = 0; enemy is null && i < 180; i++)
                {
                    Step();
                    enemy = enemies.FirstOrDefault(Approach);
                }
                if (enemy is null) throw new InvalidOperationException($"No real hook corridor for ${id:x2} in 4:{room:x2}: " + string.Join(",", enemies.Select(e => e.Position)));
                Step(); // Native initialization precedes the throw.
                int health = enemy.Health;
                Step(press: true);
                var hook = _entities.SwitchHook!.Item!;
                int ticks = 0;
                while (!(bool)pending.GetValue(hook)! && hook.State == 1 && ticks++ < 40) Step();
                FailIf(!(bool)pending.GetValue(hook)! || hook.State != 1 || !hook.CollisionEnabled ||
                    enemy.Health != health || _entities.SwitchHook.ExchangeActive,
                    $"${id:x2} did not deflect through its native corridor: hook={hook.Position}/{hook.State}, enemy={enemy.Position}.");
                FailIf(enemy.InvincibilityCounter != (id == 0x0e ? -20 : id == 0x4d ? -21 : 0) ||
                    enemy.KnockbackCounter != (id == 0x4d ? 11 : 0),
                    $"${id:x2} lost its collision damage-table counters.");
                var clink = id == 0x0e ? _entities.Entities<ClinkEffect>().Single() : null;
                FailIf(clink is not null && (clink.Visible || clink.ElapsedFrames != 0),
                    "Late collision clink initialized before the next interaction pass.");
                Vector2 hit = enemy.Position, tip = hook.Position;
                // Source state1 tests the nonzero collision signal before
                // bit5 cancellation, so a simultaneous cancel still retracts.
                if (batch) hook.RequestCancellation();
                Step();
                FailIf(hook.State != 2 || hook.Position != tip || enemy.Health != health ||
                    (id == 0x4d ? enemy.Position != hit : enemy.Position == hit),
                    $"${id:x2} lost its JUST_HIT movement/retraction behavior.");
                FailIf(enemy.InvincibilityCounter != (id == 0x0e ? -19 : id == 0x4d ? -20 : 0) ||
                    enemy.KnockbackCounter != (id == 0x4d ? 11 : 0),
                    $"${id:x2} advanced the wrong counter on the pending-hit update.");
                FailIf(clink is not null && (!clink.Visible || clink.ElapsedFrames != 1 || clink.AnimationFrame != 0),
                    "Clink state0 must show its initial frame without advancing animation.");
                if (batch) Step(10); else for (int i = 0; i < 10; i++) Step();
                FailIf(id == 0x4d && (enemy.KnockbackCounter != 1 || enemy.Position == hit),
                    "Hardhat hook recoil did not move for ten updates and retain its eleventh.");
                Step();
                FailIf(enemy.KnockbackCounter != 0 || enemy.Health != health || _entities.SwitchHook.ExchangeActive,
                    $"${id:x2} lost health or retained recoil after its source boundary.");
                var result = (enemy.Position, enemy.InvincibilityCounter, enemy.KnockbackCounter, hook.State);
                while (_player.IsUsingSwitchHook && ticks++ < 160) Step();
                // The trap keeps charging and may subsequently strike Link.
                // That normal contact recoil is independent of hook exchange.
                FailIf(_player.IsUsingSwitchHook || _entities.SwitchHook.ExchangeActive,
                    $"${id:x2} deflection failed to return control without exchange.");
                if (repeat)
                {
                    for (int i = 0; (enemy.InvincibilityCounter != 0 || _player.KnockbackFrames > 0) && i < 40; i++) Step();
                    isolateTarget = false;
                    bool reachable = Approach(enemy);
                    for (int i = 0; !reachable && i < 180; i++) { Step(); reachable = Approach(enemy); }
                    FailIf(!reachable, $"${id:x2} lost a reachable corridor for the second throw.");
                    Step(press: true);
                    var second = _entities.SwitchHook.Item!;
                    FailIf(second == hook, $"${id:x2} prevented the second hook action.");
                    for (int i = 0; !second.Finished && i < 120; i++) Step();
                    FailIf(!second.Finished || enemy.Health != health || _entities.SwitchHook.ExchangeActive,
                        $"${id:x2} repeated deflection failed to complete without exchange/damage.");
                }
                return result;
            }
            FailIf(Run(false, true) != Run(true, false), $"Batched hook deflection differs for ${id:x2}.");
        }
    }
}
