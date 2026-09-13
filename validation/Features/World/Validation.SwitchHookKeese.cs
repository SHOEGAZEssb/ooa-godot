using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookKeese()
    {
        FailIf(SwitchHookCollisionDatabase.Shared.Effect(0x1f) != 8,
            "Keese mode $1f must select hook damage effect $08.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Vector2 movement = default, bool fire = false)
        {
            input.CaptureForValidation(fire ? ["attack"] : [], fire ? ["attack"] : [], movement);
            scheduler.Advance(count / 60.0, update);
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        foreach (bool batch in new[] { false, true })
        {
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            _player.SetBraceletLiftCollisionsDisabled(true);
            for (int i = 0; _player.Position.Y > 112 && i < 30; i++) Step(movement: Vector2.Up);
            FailIf(_player.Position.Y != 112 || _currentRoom.IsSolid(_player.Position),
                "Keese hook fixture must approach on the actual entrance floor.");
            Vector2 origin = _player.PrecisePosition;
            // Native enemy $32:$00, placed on clear floor to isolate its collision
            // status from room-placement RNG and other moving targets.
            for (int repetition = 0; repetition < 2; repetition++)
            {
                FailIf(!_entities.TrySpawnEnemy(0x32, 0, new Vector2(120, 80), "Keese hook regression", out string error), error);
                var bat = _entities.Entities<KeeseCharacter>().Single();
                FailIf(bat.Health != 1 || bat.Record.CollisionRadiusX != 6 || bat.Record.CollisionRadiusY != 4,
                    "Native Keese enemyData $9d/$9f/$07/$00 must retain 1 HP and radii 6/4.");
                Step(fire: true);
                var hook = _entities.SwitchHook!.Item!;
                for (int i = 0; bat.Health != 0 && i < 30; i++) Step();
                FailIf(bat.Health != 0 || bat.IsDead || !bat.PendingKnockbackDeath || bat.CollisionEnabled ||
                    bat.KnockbackCounter != 8 || bat.InvincibilityCounter != 16 || hook.State != 1 ||
                    _entities.SwitchHook.ExchangeActive,
                    "Keese hook collision must apply effect08 and defer recoil/death to the enemy handler.");
                Vector2 hit = bat.Position;
                int rest = bat.Counter1;
                Step();
                FailIf(bat.Position != hit || bat.Counter1 != rest || bat.KnockbackCounter != 8 ||
                    bat.InvincibilityCounter != 15 || hook.State != 2,
                    "ENEMYSTATUS_JUST_HIT must pause Keese motion/recoil and retract the hook next update.");
                if (batch) Step(7); else for (int i = 0; i < 7; i++) Step();
                FailIf(bat.IsDead || bat.KnockbackCounter != 1 || bat.Position != hit + Vector2.Up * 14,
                    "Keese must recoil at SPEED_200 for seven updates before its last movement.");
                Step();
                FailIf(bat.IsDead || bat.KnockbackCounter != 0 || bat.Position != hit + Vector2.Up * 16,
                    "Keese's eighth recoil update must precede its health-zero dispatch.");
                Step();
                FailIf(!bat.IsDead || _entities.Entities<EnemyDeathPuffEffect>().Count == 0 ||
                    _player.PrecisePosition != origin || _entities.SwitchHook.ExchangeActive,
                    "Keese must die through its native puff without exchanging Link.");
                for (int i = 0; !hook.Finished && i < 80; i++) Step();
                FailIf(!hook.Finished, "Keese hit did not release the parent for another hook action.");
                Step(40);
            }
        }
    }
}
