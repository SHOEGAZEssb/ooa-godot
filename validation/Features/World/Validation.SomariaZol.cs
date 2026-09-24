using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaZol()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int scenario in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9d);
            _player.ApplicationUpdateOwned = true;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            var zol = _entities.Entities<ZolCharacter>().First(e => e.Record.SubId == (scenario == 0 ? 0 : 1));
            // Use this room's actual floor to wake its native green Zol.
            Vector2 approach = zol.Position + Vector2.Down * 24;
            FailIf(_collision.Collides(approach), "Crown Zol fixture needs an open floor approach.");
            _player.WarpTo(approach);
            Step(2);
            for (int i = 0; !zol.CollisionEnabled && i < 120; i++) Step();
            FailIf(!zol.CollisionEnabled || zol.ZFixed != 0 || zol.Record.RawDamage != 0xfc,
                "Zol must emerge normally; extraEnemyData supplies raw damage$fc.");
            Vector2 point = zol.Position.Floor() + Vector2.Left * 16;
            FailIf(_currentRoom.IsSolid(point), "Somaria fixture must retain the room's real floor.");
            foreach (var enemy in _entities.Entities<EnemyCharacter>())
            {
                if (enemy != zol) enemy.Position = new(24, 24);
                enemy.InvincibilityCounter = 127;
            }
            FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Zol fixture must allocate ITEM$18.");
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            Step(10);
            zol.Position = point + Vector2.Right * 8;
            zol.InvincibilityCounter = 0;
            if (scenario == 2) zol.Health = 5; // Controlled survivor exercises red split ordering.
            int health = zol.Health;
            int gels = _entities.Entities<GelCharacter>().Count;
            Step();
            FailIf(zol.Health != System.Math.Max(0, health - 4) || zol.InvincibilityCounter != 21 ||
                zol.KnockbackCounter != 11 || !zol.DamageHitPending || zol.IsDead ||
                block.Health != 9 || block.DamageToApply != -4,
                $"Somaria effect$2f scenario{scenario}: hp={zol.Health}, inv={zol.InvincibilityCounter}, recoil={zol.KnockbackCounter}, pending={zol.DamageHitPending}, dead={zol.IsDead}, Z={zol.ZFixed}, state={zol.State}, pos={zol.Position}, block={block.Position}/{block.State}/{block.Health}/{block.DamageToApply}.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step(2);
                FailIf(!zol.DamageHitPending || zol.KnockbackCounter != 11 || block.Health != 9,
                    "Dialogue must retain the pending Zol/block collision.");
            }
            finally { _entities.TextActiveSource = text; }
            Vector2 struckAt = zol.Position;
            Step();
            FailIf(zol.Position != struckAt || zol.DamageHitPending || zol.InvincibilityCounter != 20 ||
                zol.KnockbackCounter != 11 || block.Health != 5 || block.DamageToApply != 0 ||
                scenario != 0 && zol.State != ZolState.RedSplitting,
                "JUST_HIT must select red split state without consuming recoil; block damage applies once.");
            Step(11);
            FailIf(zol.IsDead || zol.KnockbackCounter != 0 || zol.InvincibilityCounter != 9 ||
                _entities.Entities<GelCharacter>().Count != gels ||
                _entities.Entities<KillEnemyPuffEffect>().Count != 0,
                "All eleven recoil updates must precede death or red split dispatch.");
            Step();
            if (scenario != 2)
            {
                FailIf(_entities.Entities<ZolCharacter>().Contains(zol) ||
                    _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
                    _entities.Entities<GelCharacter>().Count != gels,
                    "Native-health green/red Zols must die after block recoil without splitting.");
            }
            else
            {
                FailIf(zol.State != ZolState.RedSplitDelay || zol.Counter2 != 18 || zol.Visible ||
                    _entities.Entities<KillEnemyPuffEffect>().Count != 1,
                    "A surviving red Zol must start its source eighteen-update split delay after recoil.");
                Step(17);
                FailIf(zol.Counter2 != 1 || _entities.Entities<GelCharacter>().Count != gels,
                    "The split must not allocate children before delay update18.");
                Step();
                FailIf(_entities.Entities<ZolCharacter>().Contains(zol) ||
                    _entities.Entities<GelCharacter>().Count != gels + 2,
                    "The surviving red Zol must produce two Gels on delay update18.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
