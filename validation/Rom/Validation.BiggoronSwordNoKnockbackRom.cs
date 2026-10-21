using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBiggoronSwordNoKnockbackRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x43);
            _inventory.EquipA(0);
            _inventory.EquipB(0);
            var target = _entities.Entities<RiverZoraCharacter>().First();
            foreach (var other in _entities.EntityAdapters<IRoomEntity>()
                .Where(entity => !ReferenceEquals(entity.Node, target)).ToArray())
                _entities.RetireEntity(other);
            int slot = Enumerable.Range(0, 16).Single(index =>
                ReferenceEquals(_entities.ResolveEnemySlot(index)?.Node, target));
            int address = 0xd080 + slot * 0x100;
            _player.WarpTo(new(8, 120), recordSafe: false);
            _player.ApplicationUpdateOwned = true;
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 8, 120);
            native[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x08 * 4];
            native[address] = 1;
            native[address + 1] = 0x08;
            native.Word(address + 0x0a, (int)(target.Position.Y * 256));
            native.Word(address + 0x0c, (int)(target.Position.X * 256));

            // Run native state zero, water search and surfacing independently;
            // the source sprite is resident, and neither fixture attacks yet.
            int updates = 0;
            while (target.State != 0x0b && updates++ < 1024)
            {
                StepGameplayUpdates(1, Vector2.Zero);
                native.AdvanceEnemies(_entities.FrameCounter);
                FailIf(target.State != native[address + 4] ||
                    target.Counter != native[address + 6] ||
                    target.Position != new Vector2(native.Word(address + 0x0c) / 256.0f,
                        native.Word(address + 0x0a) / 256.0f),
                    "ENEMY$08 Biggoron fixture must match native initialization and water surfacing.");
            }
            FailIf(!target.CollisionEnabled || native[address + 0x24] != 0x88,
                "ENEMY$08 must surface before testing ITEMCOLLISION_BIGGORON_SWORD $07.");

            // Declare flat contact geometry after the real water search. This
            // isolates collision/status behavior, not a natural bank approach.
            FillValidationRoom(0x0c, width: 10, height: 8);
            native.CopyRoom(_currentRoom);
            _player.WarpTo(new(80, 64), recordSafe: false);
            _player.Face(Vector2I.Up);
            native[0xd00b] = 64;
            native[0xd00d] = 80;
            target.Position = new(99, 66);
            native.Word(address + 0x0a, 66 * 256);
            native.Word(address + 0x0c, 99 * 256);
            target.Health = native[address + 0x29] = 40;
            _inventory.GiveTreasure(TreasureId.BiggoronSword, 1);
            _inventory.EquipA(TreasureId.BiggoronSword);
            var sounds = _sound.AttachPlayRequestAudit();

            void Compare()
            {
                // Execute the actual native enemy walk and collision scan. The
                // source attack pose is the observed production reserved ITEM;
                // target health/status/animation are never resynchronized.
                native.AdvanceEnemies(_entities.FrameCounter);
                native[0xd600] = 0;
                if (_entities.Biggoron!.Weapon is { } sword)
                {
                    native[0xd600] = 1;
                    native[0xd601] = 0x0c;
                    native[0xd604] = (byte)sword.State;
                    native[0xd60b] = (byte)sword.Position.Y;
                    native[0xd60d] = (byte)sword.Position.X;
                    native[0xd60f] = unchecked((byte)sword.ZHigh);
                    native[0xd624] = (byte)sword.Collision;
                    native[0xd626] = (byte)sword.Radius.Y;
                    native[0xd627] = (byte)sword.Radius.X;
                    native[0xd628] = unchecked((byte)-sword.Damage);
                }
                native.AdvanceEnemyAndPartCollisions();
                FailIf(target.IsDead != (native[address] == 0),
                    "ENEMY$08 no-knockback death must occur on the native status update.");
                if (target.IsDead) return;
                FailIf(target.Health != native[address + 0x29] ||
                    target.InvincibilityCounter != unchecked((sbyte)native[address + 0x2b]) ||
                    target.KnockbackCounter != native[address + 0x2d] ||
                    target.KnockbackAngle != native[address + 0x2c] ||
                    target.NativeHitPending != ((native[address + 0x2a] & 0x80) != 0) ||
                    target.CollisionEnabled != ((native[address + 0x24] & 0x80) != 0) ||
                    target.AnimationParameter != native[address + 0x21],
                    $"ENEMY$08 effect$0b batch={batched}: health={target.Health}/{native[address + 0x29]}, " +
                    $"inv={target.InvincibilityCounter}/{native[address + 0x2b]}, " +
                    $"hit={target.NativeHitPending}/${native[address + 0x2a]:x2}, " +
                    $"recoil={target.KnockbackAngle}:{target.KnockbackCounter}/" +
                    $"{native[address + 0x2c]}:{native[address + 0x2d]}.");
            }
            void Step(int count, bool attack = false) => StepGameplayUpdates(count, Vector2.Zero,
                attack ? ["attack"] : [], attack ? ["attack"] : [],
                batched: batched, afterUpdate: Compare);

            Step(1, attack: true);
            // collisionEffects.s:collisionEffect0b -> ENEMYDMG_0c=$f1,$20,$00,$00.
            FailIf(target.Health != 35 || target.InvincibilityCounter != 32 ||
                target.KnockbackCounter != 0 || !target.NativeHitPending,
                "ITEM$0c must deal five damage, publish JUST_HIT and grant$20 invincibility with zero recoil.");
            Step(3);
            _dialogue.ShowMessage("No-knockback hit pause.", 64);
            native[0xcba0] = 1;
            Step(3);
            _dialogue.Close();
            native[0xcba0] = 0;
            Step(31);
            FailIf(_entities.Biggoron!.Active || target.NativeHitPending ||
                target.InvincibilityCounter != 0 || target.Health != 35,
                "ENEMY$08 must recover without a repeated hit and allow another completed swing.");

            // A repeated ordinary attack now exercises a lethal hit, including
            // the intervening JUST_HIT update before native enemyDie.
            target.Health = native[address + 0x29] = 5;
            Step(1, attack: true);
            FailIf(target.Health != 0 || target.IsDead || target.CollisionEnabled,
                "A lethal effect$0b must disable collision while retaining its deferred status owner.");
            Step(1);
            FailIf(target.IsDead, "ENEMY$08 JUST_HIT must precede health-zero deletion.");
            Step(1);
            FailIf(!target.IsDead || _entities.ResolveEnemySlot(slot) is not null,
                "ENEMY$08 must release its native slot after the following health-zero dispatch.");
            FailIf(sounds.Requests.Count(sound => sound == SoundId.SndDamageEnemy) != 2 ||
                native.Sounds.Count(sound => sound == SoundId.SndDamageEnemy) != 2,
                "Each accepted effect$0b contact must request exactly one native damage sound.");
            Step(34);
            FailIf(_entities.Biggoron!.Active || _entities.Biggoron.Weapon is not null,
                "The lethal target must not prevent the attacking ITEM$0c from completing.");
        }
        ReinitializeGameplayForValidation();
    }
}
