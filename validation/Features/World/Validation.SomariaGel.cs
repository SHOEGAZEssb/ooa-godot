using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaGel()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool survives in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9d);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _player.WarpTo(new(136, 128));
            Vector2 point = new(120, 104);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.IsSolid(point),
                "Gel/block fixture requires Crown4:9d's unchanged floor.");
            // Use the same production spawn as the room's red Zol children.
            var gel = _entities.Spawn<GelCharacter>(new GelSpawn(point + Vector2.Right * 4));
            FailIf(gel.Definition.RawDamage != 0xfc || gel.Health != 1,
                "enemyData$43 -> extraEnemyData$06 supplies Gel damage$fc and health1.");
            // A long wait isolates ordinary AI counters from random movement.
            gel.SetStateForValidation(GelState.Waiting, counter1: 50);
            if (survives) gel.Health = 5;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Gel fixture must allocate ITEM$18.");
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy);
            Step(9);
            FailIf(gel.Health != (survives ? 5 : 1), "Phase-in must reject Gel contact before update10.");
            Step();
            FailIf(gel.Health != (survives ? 1 : 0) || gel.IsDead || !gel.NativeHitPending ||
                gel.InvincibilityCounter != 21 || gel.KnockbackCounter != 11 || block.Health != 9 ||
                block.DamageToApply != -4 || _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != sounds + 1,
                "Gel block contact must publish effect$2f damage and counters without immediate death.");
            Vector2 hitPosition = gel.Position;
            int counter = gel.Counter1;
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step(2);
                FailIf(gel.Counter1 != counter || !gel.NativeHitPending || block.Health != 9,
                    "Text freeze must preserve the Gel/block pending exchange.");
            }
            finally { _entities.TextActiveSource = text; }
            Step();
            FailIf(gel.NativeHitPending || gel.Counter1 != counter - 1 || gel.Position != hitPosition ||
                gel.InvincibilityCounter != 20 || gel.KnockbackCounter != 11 || block.Health != 5,
                "gel.s runs normal AI on JUST_HIT, while the block consumes damage once.");
            Step(11);
            FailIf(gel.IsDead || gel.Counter1 != counter - 12 || gel.Position != hitPosition ||
                gel.InvincibilityCounter != 9 || gel.KnockbackCounter != 0,
                "Gel must run eleven normal AI updates through recoil status without recoil movement or early death.");
            Step();
            if (survives)
                FailIf(!_entities.Entities<GelCharacter>().Contains(gel) || gel.Counter1 != counter - 13 ||
                    gel.Health != 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
                    "A surviving Gel must continue normal AI after recoil status clears.");
            else
                FailIf(_entities.Entities<GelCharacter>().Contains(gel) ||
                    _entities.Entities<EnemyDeathPuffEffect>().Count != 1,
                    "Native-health Gel death must follow the final recoil-status update.");
        }
        ReinitializeGameplayForValidation();
    }
}
