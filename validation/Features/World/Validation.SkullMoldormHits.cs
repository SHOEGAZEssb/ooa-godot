using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormHitTiming()
    {
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false) =>
                StepGameplayUpdates(count, movement, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            foreach (string scenario in new[] { "active", "cancelled", "room-change" })
            {
                LoadValidationRoom(4, 0x91);
                _player.WarpTo(new Vector2(120, 128));
                Step(16, Vector2.Up);
                FailIf(_player.Position != new Vector2(120, 112), "Moldorm spawn-hit fixture must approach through actual entrance geometry.");
                _player.SetBraceletLiftCollisionsDisabled(true);
                FailIf(!_entities.TrySpawnEnemy(EnemyId.Moldorm, 0, new Vector2(120, 80), "Moldorm melee initialization window", out string error), error);
                bool active = true;
                // The weapon request precedes the ENEMY pass. Its collision
                // scan must include head/tails that do not exist yet.
                _entities.ApplySwordHit(new Rect2(112, 72, 16, 16), _player.Position, 2,
                    EnemyKnockbackStrength.Low, meleeActive: () => active);
                if (scenario == "cancelled") active = false;
                if (scenario == "room-change")
                {
                    LoadValidationRoom(4, 0x91);
                    _player.WarpTo(new Vector2(120, 112));
                    _player.SetBraceletLiftCollisionsDisabled(true);
                    FailIf(!_entities.TrySpawnEnemy(EnemyId.Moldorm, 0, new Vector2(120, 80), "Moldorm after cancelled room request", out error), error);
                }
                Step();
                var head = _entities.Entities<MoldormCharacter>().Single();
                var tails = _entities.Entities<MoldormTailCharacter>();
                bool hit = scenario == "active";
                FailIf(head.State != 8 || head.Health != (hit ? 6 : 8) || tails.Count != 2 ||
                    tails.Any(tail => tail.State != 8 || tail.Health != (hit ? 6 : 8) || !tail.CollisionEnabled),
                    $"{scenario}: native state-zero head/tails must expose their same-update collision window and respect weapon cancellation.");
                Step();
                FailIf(tails.Any(tail => tail.State != 9 || tail.CollisionEnabled) ||
                    hit && (head.State != 8 || head.InvincibilityCounter != 15 || head.KnockbackCounter != 8 ||
                        tails.Any(tail => tail.InvincibilityCounter != 15 || tail.KnockbackCounter != 8)),
                    "Head JUST_HIT must return before recoil, while tail JUST_HIT falls through to state8 and disables its collision.");
                FailIf(!_entities.TrySpawnEnemy(EnemyId.Moldorm, 0, new Vector2(120, 80), "Moldorm after melee request expiry", out error), error);
                Step();
                FailIf(_entities.Entities<MoldormCharacter>().Count != 2 ||
                    _entities.Entities<MoldormCharacter>().Where(h => h != head).Single().Health != 8,
                    "A resolved or cancelled melee request must not affect actors created in a later update.");
            }

            LoadValidationRoom(4, 0x91);
            if (_inventory.SwordLevel == 0) _inventory.GiveTreasure(TreasureId.Sword, 0);
            typeof(InventoryState).GetProperty(nameof(InventoryState.SwordLevel))!.SetValue(_inventory, 1);
            _inventory.EquipA(TreasureId.Sword);
            _inventory.RefillHealth();
            _inventory.ApplyDamage(4); // Isolate melee from the full-health sword beam.
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            _player.SetBraceletLiftCollisionsDisabled(true);
            FailIf(!_entities.TrySpawnEnemy(EnemyId.Moldorm, 0, new Vector2(120, 80), "Actual Moldorm sword fight", out string spawnError), spawnError);
            Step(3);
            var target = _entities.Entities<MoldormCharacter>().Single();
            void ApproachAndSwing(int tick)
            {
                Vector2 delta = target.Position - _player.Position;
                if (!_player.IsAttacking)
                    _player.Face(Math.Abs(delta.Y) >= Math.Abs(delta.X)
                        ? new Vector2I(0, Math.Sign(delta.Y)) : new Vector2I(Math.Sign(delta.X), 0));
                Step(movement: delta.Length() > 13 ? delta.Normalized() : Vector2.Zero,
                    attack: tick % 24 == 0 && delta.Length() < 28);
            }
            for (int repetition = 0; repetition < 2; repetition++)
            {
                int health = target.Health;
                for (int i = 0; target.Health == health && i < 240; i++) ApproachAndSwing(i);
                FailIf(target.Health != health - 2 || target.IsDead || target.InvincibilityCounter != 16 || target.KnockbackCounter != 8,
                    "Actual sword collision must publish ENEMYDMG_00 after movement: damage2, invincibility$10, recoil8.");
                Vector2 position = target.Position;
                int turn = target.TurnCounter;
                Step();
                FailIf(target.Position != position || target.TurnCounter != turn || target.KnockbackCounter != 8 ||
                    target.InvincibilityCounter != 15 || target.Tail1!.InvincibilityCounter != 15 || target.Tail2!.InvincibilityCounter != 15,
                    "Moldorm's sword JUST_HIT update must copy invincibility to both tails before post-update decrement, without moving or consuming recoil.");
                Step(7);
                FailIf(target.KnockbackCounter != 1 || target.TurnCounter != turn,
                    "Moldorm must perform seven subsequent recoil updates without advancing its ordinary turn counter.");
                Step();
                FailIf(target.KnockbackCounter != 0 || target.TurnCounter != turn,
                    "The eighth recoil update must complete movement and return before ordinary steering resumes.");
                Step();
                FailIf(target.TurnCounter != (turn == 1 ? 8 : turn - 1),
                    "Moldorm must resume its source steering counter on the update after recoil completes.");
                Step(8);
            }
            // Isolate the shared shield-collision writer's signed status;
            // the surrounding sword fight exercises actual player input.
            Vector2 beforeShield = target.Position;
            int shieldHealth = target.Health;
            FailIf(!target.TryApplyShieldBump(target.CollisionBounds, _player.Position, EnemyKnockbackStrength.Low) ||
                target.InvincibilityCounter != -16 || target.KnockbackCounter != 8,
                "Moldorm shield bump must publish ENEMYDMG_10's negative invincibility and recoil bytes.");
            Step();
            FailIf(target.Position != beforeShield || target.Health != shieldHealth || target.KnockbackCounter != 8 ||
                target.InvincibilityCounter != -15 || target.Tail1!.InvincibilityCounter != -15 || target.Tail2!.InvincibilityCounter != -15,
                "Shield JUST_HIT must copy signed invincibility into both tails before their post-update ticks, without damage or immediate recoil movement.");
            Step(16);
            _sound.ClearPlayRequestAudit();
            for (int i = 0; _entities.Entities<MoldormCharacter>().Count != 0 && i < 480; i++) ApproachAndSwing(i);
            FailIf(_entities.Entities<MoldormCharacter>().Count != 0 || _entities.Entities<MoldormTailCharacter>().Count != 0 ||
                _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _sound.PlayRequestsFor(SoundId.SndKillEnemy) != 1,
                "Repeated actual sword attacks must finish the head's recoil/death, delete both tails, and produce one native death puff.");
            Step(24);
            FailIf(_entities.RoomEnemyCount != 0, "Moldorm sword fight retained a tail or puff count after completion.");
        }
    }
}
