using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookStalfosAndRope()
    {
        FailIf(SwitchHookCollisionDatabase.Shared.Effect(0x14) != 8,
            "Rope's source collision mode $14 maps the hook to low-knockback damage $08.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, bool press = false)
        {
            input.CaptureForValidation(press ? ["attack"] : [], press ? ["attack"] : [], Vector2.Zero);
            scheduler.Advance(count / 60.0, update);
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        var random = CaptureOracleRandomForValidation();

        (EnemyCharacter Enemy, ISwitchHookEnemy? Target, Vector2 Origin) Prepare(bool rope)
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, rope ? 0x73 : 0x7e);
            EnemyCharacter[] enemies = rope ? _entities.Entities<RopeCharacter>().Cast<EnemyCharacter>().ToArray()
                : _entities.Entities<StalfosCharacter>().Cast<EnemyCharacter>().ToArray();
            foreach (var enemy in enemies)
            foreach (Vector2I direction in new[] { Vector2I.Down, Vector2I.Up, Vector2I.Right, Vector2I.Left })
            {
                int distance = rope ? 32 : 48; // Blue Stalfos dodge only inside Manhattan $2c.
                Vector2 origin = enemy.Position + (Vector2)direction * distance;
                if (origin.X < 12 || origin.X >= _currentRoom.Width - 12 || origin.Y < 12 || origin.Y >= _currentRoom.Height - 12)
                    continue;
                Vector2 perpendicular = new(-direction.Y, direction.X);
                bool clear = Enumerable.Range(0, distance + 1).All(i =>
                    Enumerable.Range(-5, 11).All(j =>
                    {
                        Vector2 point = enemy.Position + (Vector2)direction * i + perpendicular * j;
                        return !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None;
                    }));
                if (!clear || enemies.Any(other => other != enemy &&
                    other.Position.DistanceTo((origin + enemy.Position) / 2) < distance / 2 + 12)) continue;
                _player.WarpTo(origin);
                _player.Face(-direction);
                if (enemy is RopeCharacter falling)
                {
                    int ticks = 0;
                    while ((!falling.CollisionEnabled || falling.ZFixed != 0) && ticks++ < 150) Step();
                    FailIf(ticks >= 150, "Native falling Rope did not reach the real hook approach floor.");
                }
                return (enemy, enemy as ISwitchHookEnemy, origin);
            }
            throw new InvalidOperationException("No unobstructed native Skull enemy hook approach: " +
                string.Join(", ", enemies.Select(e => e.Position)));
        }
        SwitchHookItem Hit(ISwitchHookEnemy target)
        {
            Step(press: true);
            var item = _entities.SwitchHook!.Item!;
            for (int i = 0; !target.SwitchHookHeld && !item.Finished && i < 100; i++) Step();
            FailIf(!target.SwitchHookHeld || item.State != 1 || item.CollisionEnabled,
                $"Native enemy was not caught through its corridor: target={target.SwitchHookPosition}, hook={item.Position}, state={item.State}.");
            Step();
            FailIf(item.State != 3 || item.Substate != 0, "Pending enemy hit did not enter the item's latch on the next update.");
            return item;
        }
        {
            var fixture = Prepare(false);
            var item = Hit(fixture.Target!);
            Vector2 caught = fixture.Enemy.Position;
            int health = fixture.Enemy.Health, count = _entities.RoomEnemyCount;
            Step(18);
            FailIf(item.Substate != 1 || fixture.Enemy.CollisionEnabled || !fixture.Target!.SwitchHookHeld,
                "Stalfos/Rope failed to remain held through the latch and helper initialization.");
            Step(16);
            FailIf(fixture.Enemy.Position != caught || item.Substate != 2, "Held enemy moved before the swap dispatch.");
            Step();
            FailIf(_player.PrecisePosition != caught.Floor() ||
                fixture.Enemy.Position != fixture.Origin.Floor() + caught - caught.Floor(),
                "Stalfos/Rope exchange lost the source high-byte coordinate copies.");
            Step(16);
            FailIf(!item.Finished || fixture.Target.SwitchHookHeld || !fixture.Enemy.CollisionEnabled ||
                fixture.Enemy.Health != health || _entities.RoomEnemyCount != count,
                "Enemy release changed health/count or failed to restore collision in the landing enemy pass.");
            FailIf(((StalfosCharacter)fixture.Enemy).State != StalfosState.Deciding,
                "Native hook release returned to the wrong enemy handler.");
            Step();

            // Repeat through the same corridor. Link's facing reverses at swap.
            // Other native enemies may hit Link on landing; finish that real
            // recoil before trying another parent item.
            for (int i = 0; _player.KnockbackFrames > 0 && i < 32; i++) Step();
            var previous = item;
            Vector2 gap = (fixture.Enemy.Position.Floor() - _player.Position.Floor()).Abs();
            bool dodges = fixture.Enemy is StalfosCharacter { State: < StalfosState.Jumping } && gap.X + gap.Y < 44;
            Step(press: true);
            item = _entities.SwitchHook!.Item!;
            FailIf(item == previous || !_player.StartedItemAnimationThisUpdate ||
                dodges && ((StalfosCharacter)fixture.Enemy).State != StalfosState.Jumping,
                $"Repeated hook input lost its parent pulse/dodge: same={item == previous}, pulse={_player.StartedItemAnimationThisUpdate}, dodge={dodges}, state={(fixture.Enemy as StalfosCharacter)?.State}, knockback={_player.KnockbackFrames}, using={_player.IsUsingSwitchHook}.");
            for (int i = 0; !item.Finished && i < 160; i++) Step();
            FailIf(!item.Finished || fixture.Enemy.Health != health, "Repeated hook action failed to finish without damaging its target.");
        }

        (Vector2 Link, Vector2 Enemy, int Z, int HookSubstate) Run(bool batch)
        {
            var fixture = Prepare(false);
            var hook = Hit(fixture.Target!);
            if (batch) Step(35); else for (int i = 0; i < 35; i++) Step();
            return (_player.PrecisePosition, fixture.Enemy.Position, hook.ZHigh, hook.Substate);
        }
        FailIf(Run(false) != Run(true), "Batched updates changed the Stalfos exchange boundary.");

        (Vector2 Enemy, int Health, int Recoil, bool Dead, int HookState) DamageRope(bool batch)
        {
            var fixture = Prepare(true);
            var snake = (RopeCharacter)fixture.Enemy;
            int count = _entities.RoomEnemyCount;
            Step(press: true);
            var hook = _entities.SwitchHook!.Item!;
            for (int i = 0; snake.Health != 0 && i < 40; i++) Step();
            FailIf(snake.Health != 0 || snake.IsDead || !snake.PendingKnockbackDeath ||
                snake.KnockbackCounter != 8 || snake.InvincibilityCounter != 16 || snake.CollisionEnabled ||
                hook.State != 1 || !hook.CollisionEnabled || _entities.SwitchHook.ExchangeActive || _entities.RoomEnemyCount != count,
                "Rope hook hit must apply2 damage/$10 invincibility/$08 recoil while leaving hook enabled until its next dispatch.");
            Vector2 hitPosition = snake.Position, hookPosition = hook.Position;
            Step();
            FailIf(hook.State != 2 || hook.Position != hookPosition || snake.Position != hitPosition ||
                snake.KnockbackCounter != 8 || snake.InvincibilityCounter != 15,
                "Pending hit must retract the hook without movement and skip Rope recoil on ENEMYSTATUS_JUST_HIT.");
            if (batch) Step(7); else for (int i = 0; i < 7; i++) Step();
            FailIf(snake.IsDead || snake.KnockbackCounter != 1, "Rope recoil ended before its eighth movement update.");
            Step();
            FailIf(snake.IsDead || snake.KnockbackCounter != 0, "Zero recoil update must precede Rope's health-zero dispatch.");
            Step();
            FailIf(!snake.IsDead || _entities.Entities<EnemyDeathPuffEffect>().Count == 0 ||
                _entities.SwitchHook.ExchangeActive || _player.PrecisePosition != fixture.Origin,
                "Rope hook damage failed to create the native delayed death without exchanging Link.");
            return (snake.Position, snake.Health, snake.KnockbackCounter, snake.IsDead, hook.State);
        }
        FailIf(DamageRope(false) != DamageRope(true), "Batched updates changed Rope hook damage/recoil/death.");

        // State-machine probes complement the real gameplay path above.
        var database = new EnemyDatabase();
        var record = ResolveStalfos(database, RoomEnemyPlacements(database, 4, 0x7e, 0x31, 2)[0]);
        LoadValidationRoom(4, 0x7e);
        Vector2 center = new(120, 88);
        var skeleton = new StalfosCharacter();
        skeleton.Initialize(record, _currentRoom, center, new OracleRandom());
        skeleton.UpdateFrame(center + Vector2.Down * 20, true);
        for (int i = 0; i < 31; i++) skeleton.UpdateFrame(center);
        FailIf(skeleton.ZFixed != -992 || skeleton.SpeedZ != 480 || !skeleton.CollisionEnabled,
            "Descending Stalfos fixture lost jump update31's live Z/speedZ.");
        skeleton.BeginSwitchHook(center + Vector2.Down * 24);
        skeleton.UpdateFrame(center);
        skeleton.CopySwitchHookPosition(center, -5);
        skeleton.ReleaseSwitchHook();
        skeleton.UpdateFrame(center);
        FailIf(skeleton.ZFixed != -768 || skeleton.SpeedZ != 512 || skeleton.State != StalfosState.SwitchHook,
            "Hook release reset a descending Stalfos's retained velocity or Z fraction.");
        skeleton.BeginSwitchHook(center + Vector2.Down * 24);
        skeleton.UpdateFrame(center + Vector2.Down * 24, true);
        FailIf(skeleton.SwitchHookHeld || skeleton.State != StalfosState.Jumping || skeleton.SpeedZ != -512,
            "Stalfos held dispatch incorrectly preempted the source item-start dodge gate.");
        skeleton.Free();

    }
}
