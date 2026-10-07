using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBiggoronSwordContactsRom()
    {
        foreach (bool armos in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            EnemyCharacter target;
            int type, mode, radiusY, radiusX;
            if (armos)
            {
                _entities.Clear();
                var statue = _entities.Spawn<ArmosCharacter>(new ArmosSpawn(new(72, 72), 0xa0));
                _runtimeState.SetWramByte(OracleRuntimeState.ArmosTriggerAddress, 1);
                for (int i = 0; !statue.CollisionEnabled && i < 70; i++)
                    StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!statue.CollisionEnabled, "Biggoron target must finish native Armos activation.");
                target = statue; type = 0x1d; mode = 0x1e; radiusY = radiusX = 6;
            }
            else
            {
                var keese = _entities.Entities<KeeseCharacter>().First();
                foreach (var enemy in _entities.Entities<KeeseCharacter>())
                {
                    enemy.Position = new(24, 24);
                    enemy.InvincibilityCounter = 127;
                }
                target = keese; type = 0x32;
                mode = _entities.EntityAdapters<KeeseRoomEntity>().First().DimitriCollisionMode;
                radiusY = keese.Record.CollisionRadiusY; radiusX = keese.Record.CollisionRadiusX;
            }
            _inventory.GiveTreasure(TreasureId.BiggoronSword, 1);
            _inventory.EquipA(TreasureId.BiggoronSword);
            _player.WarpTo(new(120, 72));
            _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Biggoron attack must start on real Crown floor.");
            var native = new ObjectCollisionRom();
            bool scanned = false;
            int contacts = 0;
            var observer = new CollisionRomObserver(() =>
            {
                // Target AI publishes the declared geometry/status inputs.
                // The original scan owns ordered weapon versus Link contact,
                // health, var2a, signed invincibility and recoil publications.
                native.ClearObjects();
                const int enemy = 0xd080;
                native[enemy] = 1; native[enemy + 1] = (byte)type;
                native[enemy + 0xb] = (byte)target.Position.Y; native[enemy + 0xd] = (byte)target.Position.X;
                native[enemy + 0x24] = (byte)(type | (target.CollisionEnabled ? 0x80 : 0));
                native[enemy + 0x25] = (byte)mode;
                native[enemy + 0x26] = (byte)radiusY; native[enemy + 0x27] = (byte)radiusX;
                native[enemy + 0x29] = (byte)target.Health;
                native[enemy + 0x2a] = target.NativeHitPending ? (byte)0x87 : (byte)0;
                native[enemy + 0x2b] = unchecked((byte)target.InvincibilityCounter);
                native[enemy + 0x2c] = (byte)target.KnockbackAngle;
                native[enemy + 0x2d] = (byte)target.KnockbackCounter;
                native[enemy + 0x3e] = 1;
                scanned = true;
            });
            _entities.AddEntity(observer);
            void Compare()
            {
                if (!scanned) return;
                scanned = false;
                if (_entities.Biggoron!.Weapon is { } sword)
                {
                    native[0xd600] = 1; native[0xd601] = 0x0c; native[0xd604] = 1;
                    native[0xd60b] = (byte)sword.Position.Y; native[0xd60d] = (byte)sword.Position.X;
                    native[0xd60f] = unchecked((byte)sword.ZHigh); native[0xd624] = (byte)sword.Collision;
                    native[0xd626] = (byte)sword.Radius.Y; native[0xd627] = (byte)sword.Radius.X;
                    native[0xd628] = unchecked((byte)-sword.Damage);
                }
                native[0xd00b] = (byte)_player.Position.Y; native[0xd00d] = (byte)_player.Position.X;
                native.Call(ObjectCollisionRom.Scan);
                contacts += native.Dispatches.Count(entry => entry.Type == 7);
                FailIf(target.Health != native[0xd0a9] ||
                    target.NativeHitPending != ((native[0xd0aa] & 0x80) != 0) ||
                    target.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) ||
                    target.KnockbackAngle != native[0xd0ac] || target.KnockbackCounter != native[0xd0ad],
                    $"Biggoron ENEMY${type:x2} contact: health {target.Health}/{native[0xd0a9]}, " +
                    $"pending {target.NativeHitPending}/${native[0xd0aa]:x2}, inv {target.InvincibilityCounter}/{unchecked((sbyte)native[0xd0ab])}, " +
                    $"recoil {target.KnockbackAngle}/{native[0xd0ac]}:{target.KnockbackCounter}/{native[0xd0ad]}.");
            }
            var cues = _sound.AttachPlayRequestAudit();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // Facing up starts at source arc$02: Y+$02, X+$13.
                target.Position = _player.Position + new Vector2(19, 2);
                target.Health = 40; target.InvincibilityCounter = 0;
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], afterUpdate: Compare);
                FailIf(target.Health != 35 || !target.NativeHitPending ||
                    target.InvincibilityCounter != (armos ? 0x10 : 0x1a) ||
                    target.KnockbackCounter != (armos ? 8 : 15),
                    $"Biggoron ENEMY${type:x2} must publish its source low/high hit profile after AI.");
                StepGameplayUpdates(34, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(_entities.Biggoron!.Active || _entities.Biggoron.Weapon != null,
                    "Biggoron contact must retain normal parent completion and permit recasting.");
            }
            FailIf(contacts != 2 || cues.Requests.Count(id => id == SoundId.SndDamageEnemy) != 2,
                $"Biggoron ENEMY${type:x2} must accept one native damage contact/cue per swing.");
        }
        ReinitializeGameplayForValidation();
    }
}
