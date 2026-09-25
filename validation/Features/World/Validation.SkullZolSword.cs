using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullZolSword()
    {
        var random = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        foreach (string scenario in new[] { "green", "red", "red-lethal" })
        {
            void Step(int count = 1, Vector2 movement = default, bool fire = false) =>
                StepGameplayUpdates(count, movement, fire ? ["attack"] : [], fire ? ["attack"] : [], batched: batch);
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x91);
            if (_inventory.SwordLevel == 0) _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 0);
            typeof(InventoryState).GetProperty(nameof(InventoryState.SwordLevel))!.SetValue(
                _inventory, scenario == "red-lethal" ? 2 : 1);
            _inventory.EquipA(InventoryState.ItemSword);
            _inventory.RefillHealth();
            _inventory.ApplyDamage(4); // Keep the collision attributable to melee, without a full-health sword beam.
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Sword approach must walk along the actual D4 entrance floor.");
            _player.SetBraceletLiftCollisionsDisabled(true);
            FailIf(!_entities.TrySpawnEnemy(0x34, scenario == "green" ? 0 : 1, new Vector2(120, 80), "Skull Zol sword regression", out string error), error);
            var zol = _entities.Entities<ZolCharacter>().Single();
            for (int i = 0; !zol.CollisionEnabled && i < 100; i++) Step();
            FailIf(!zol.CollisionEnabled, "Zol sword target must initialize/emerge naturally before combat.");
            int health = zol.Health;
            _sound.ClearPlayRequestAudit();
            for (int i = 0; zol.Health == health && i < 160; i++)
            {
                Vector2 delta = zol.Position - _player.Position;
                if (!_player.IsAttacking)
                    _player.Face(Math.Abs(delta.Y) >= Math.Abs(delta.X)
                        ? new Vector2I(0, Math.Sign(delta.Y)) : new Vector2I(Math.Sign(delta.X), 0));
                Step(movement: delta.Length() > 13 ? delta.Normalized() : Vector2.Zero,
                    fire: i % 24 == 0 && delta.Length() < 28);
            }
            bool split = scenario == "red";
            FailIf(zol.Health != (split ? 1 : 0) || zol.IsDead || zol.State is ZolState.RedSplitting or ZolState.RedSplitDelay ||
                zol.InvincibilityCounter != 32 || zol.KnockbackCounter != 0 ||
                _entities.Entities<EnemyDeathPuffEffect>().Count != 0 || _entities.Entities<KillEnemyPuffEffect>().Count != 0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 1,
                $"Actual {scenario} sword collision must publish damage after ENEMY dispatch and retain JUST_HIT without immediate death or split: hp={zol.Health}, state={zol.State}, inv={zol.InvincibilityCounter}, recoil={zol.KnockbackCounter}, dead={zol.IsDead}, sound={_sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy)}, Link={_player.Position}, Zol={zol.Position}, sword={_player.SwordState}/{_player.SwordStateFrame}, item={_inventory.EquippedA}.");
            var oldState = zol.State;
            Vector2 hitPosition = zol.Position;
            int counter = zol.Counter1;
            Step();
            FailIf(zol.IsDead || zol.Position != hitPosition || zol.Counter1 != counter || zol.InvincibilityCounter != 31 ||
                zol.State != (scenario == "green" ? oldState : ZolState.RedSplitting),
                "Zol JUST_HIT must return before ordinary movement/counters, selecting red stateC for the next update.");
            Step();
            if (split)
            {
                FailIf(zol.State != ZolState.RedSplitDelay || zol.Counter2 != 18 || zol.Visible || zol.CollisionEnabled ||
                    _entities.Entities<KillEnemyPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
                    "Surviving red Zol must begin its source18-update split after the separate JUST_HIT handler.");
                Step(17);
                FailIf(zol.Counter2 != 1 || _entities.Entities<GelCharacter>().Count != 0, "Red Zol split ended before18 updates.");
                Step();
                FailIf(_entities.Entities<ZolCharacter>().Count != 0 || _entities.Entities<GelCharacter>().Count != 2 ||
                    !_entities.Entities<GelCharacter>().Select(gel => gel.Position).ToHashSet().SetEquals(
                        new[] { hitPosition + Vector2.Right * 4, hitPosition + Vector2.Left * 4 }),
                    "Surviving sword-hit red Zol must replace itself with the two source +/-4 X Gels.");
            }
            else
            {
                FailIf(_entities.Entities<ZolCharacter>().Count != 0 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
                    _entities.Entities<KillEnemyPuffEffect>().Count != 0 || _entities.Entities<GelCharacter>().Count != 0,
                    "Lethal green/red sword hit must dispatch enemyDie before red stateC can create its split puff.");
                Step(24);
                FailIf(_entities.Entities<GelCharacter>().Count != 0, "Lethally struck red Zol created delayed replacement Gels.");
            }
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 1,
                "Zol sword death/split must play one SND_KILLENEMY through its selected source path.");
        }
    }
}
