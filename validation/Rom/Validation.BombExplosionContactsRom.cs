using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombExplosionContactsRom(bool bombchu)
    {
        foreach (bool armos in new[] { false, true })
        foreach (int health in armos ? new[] { 40, 4 } : new[] { 40 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xa1);
            EnemyCharacter target;
            int type, mode;
            if (armos)
            {
                _entities.Clear();
                var statue = _entities.Spawn<ArmosCharacter>(new ArmosSpawn(new(72, 72), 0xa0));
                _runtimeState.SetWramByte(OracleRuntimeState.ArmosTriggerAddress, 1);
                for (int wait = 0; !statue.CollisionEnabled && wait < 70; wait++) StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!statue.CollisionEnabled, "Bombchu Armos target must complete source activation.");
                target = statue; type = 0x1d; mode = 0x1e;
            }
            else
            {
                target = _entities.Entities<KeeseCharacter>().First(); type = 0x32;
                mode = _entities.EntityAdapters<KeeseRoomEntity>().First().DimitriCollisionMode;
                foreach (var keese in _entities.Entities<KeeseCharacter>())
                { keese.Position = new(24, 24); keese.InvincibilityCounter = 127; }
            }
            var treasure = bombchu ? TreasureId.Bombchus : TreasureId.Bombs;
            _inventory.GiveTreasure(treasure, 0x10); _inventory.EquipA(treasure); _inventory.EquipB(0);
            _player.WarpTo(new(120, 72)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Bombchu contact must start on actual Crown floor.");
            var native = new ObjectCollisionRom();
            var cues = _sound.AttachPlayRequestAudit();
            bool scanned = false; int contacts = 0;
            _entities.AddEntity(new CollisionRomObserver(() =>
            {
                native.ClearObjects(); const int slot = 0xd080;
                native[slot] = 1; native[slot + 1] = (byte)type;
                native[slot + 0xb] = (byte)target.Position.Y; native[slot + 0xd] = (byte)target.Position.X;
                native[slot + 0x24] = (byte)(type | (target.CollisionEnabled ? 0x80 : 0)); native[slot + 0x25] = (byte)mode;
                native[slot + 0x26] = (byte)(target.CollisionBounds.Size.Y / 2); native[slot + 0x27] = (byte)(target.CollisionBounds.Size.X / 2);
                native[slot + 0x29] = (byte)target.Health; native[slot + 0x2a] = target.NativeHitPending ? (byte)0x98 : (byte)0;
                native[slot + 0x2b] = unchecked((byte)target.InvincibilityCounter);
                native[slot + 0x2c] = (byte)target.KnockbackAngle; native[slot + 0x2d] = (byte)target.KnockbackCounter;
                native[slot + 0x3e] = 1; scanned = true;
            }));
            void Compare()
            {
                if (!scanned) return; scanned = false;
                if (_entities.Entities<BombEffect>().SingleOrDefault() is { ExplosionCollisionEnabled: true } item)
                {
                    native[0xd700] = 1; native[0xd701] = (byte)(bombchu ? 0x0d : 0x03); native[0xd704] = 0xff;
                    native[0xd70b] = (byte)item.Position.Y; native[0xd70d] = (byte)item.Position.X;
                    native[0xd70f] = unchecked((byte)item.CollisionZ); native[0xd724] = 0x98;
                    native[0xd726] = native[0xd727] = (byte)item.ExplosionRadius; native[0xd728] = unchecked((byte)-item.Damage);
                }
                native[0xd00b] = (byte)_player.Position.Y; native[0xd00d] = (byte)_player.Position.X;
                native.Call(ObjectCollisionRom.Scan);
                contacts += native.Dispatches.Count(entry => entry.Type == 0x18);
                FailIf(target.Health != native[0xd0a9] || target.NativeHitPending != ((native[0xd0aa] & 0x80) != 0) ||
                    target.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) || target.KnockbackCounter != native[0xd0ad] ||
                    target.KnockbackAngle != native[0xd0ac],
                    $"ITEM${(bombchu ? 0x0d : 0x03):x2} ENEMY${type:x2} mode${mode:x2} contact: health={target.Health}/{native[0xd0a9]}, " +
                    $"pending={target.NativeHitPending}/${native[0xd0aa]:x2}, inv={target.InvincibilityCounter}/{native[0xd0ab]}, " +
                    $"recoil={target.KnockbackCounter}/{native[0xd0ad]} angle={target.KnockbackAngle}/{native[0xd0ac]}.");
            }
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], afterUpdate: Compare);
            StepGameplayUpdates(bombchu ? 16 : 19, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (!bombchu)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], afterUpdate: Compare);
                StepGameplayUpdates(9, Vector2.Zero, batched: batched, afterUpdate: Compare);
                StepGameplayUpdates(32, Vector2.Down, batched: batched, afterUpdate: Compare);
            }
            var child = _entities.Entities<BombEffect>().Single();
            target.Position = child.Position; target.Health = health; target.InvincibilityCounter = 0;
            // Declare the fuse boundary; real item update, AI, interactions
            // and post-object collision dispatch still own this update.
            if (bombchu)
                typeof(BombchuItem).GetField("_counter2", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(child, 1);
            else
            {
                // animationData19f5e entry$09 precedes the explosion marker$0a.
                typeof(BombEffect).GetField("_frameIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(child, 9);
                typeof(BombEffect).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(child, 1);
            }
            StepGameplayUpdates(1, Vector2.Zero, afterUpdate: Compare);
            FailIf(contacts != 1 || target.Health != health - 4,
                $"ITEM${(bombchu ? 0x0d : 0x03):x2} explosion must apply four-point damage once: contacts={contacts}, health={target.Health}/{health - 4}, state={child.State}.");
            if (health == 4)
                CompareLethalArmosBombStatusRom(target, native, batched);
            else
                StepGameplayUpdates(6, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(contacts != 1, "Bombchu explosion must respect the target's retained invincibility.");
            FailIf(cues.Requests.Count(cue => cue == SoundId.SndDamageEnemy) != 1,
                "ENEMYDMG_$08/$0c must emit exactly one damage cue for the accepted Bombchu contact.");
        }
        GD.Print($"Compared actual Keese/Armos ITEM${(bombchu ? 0x0d : 0x03):x2} explosion contacts after AI through native ITEMCOLLISION_BOMB scanning: health, JUST_HIT, invincibility, high/no-knockback and lethal Armos status; target AI outputs and the fuse boundary are declared inputs.");
    }

    private void CompareLethalArmosBombStatusRom(EnemyCharacter target, ObjectCollisionRom collision, bool batched)
    {
        var native = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        for (int offset = 0; offset < 0x40; offset++) native[0xd080 + offset] = collision[0xd080 + offset];
        native[0xd080] = 0x11; native[0xd082] = 0xa0; native[0xd084] = 0x0b;
        FailIf(target.IsDead || !target.PendingKnockbackDeath || target.CollisionEnabled,
            "ENEMYDMG_0c must disable zero-health Armos collision and retain its pending death.");
        StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
        {
            native.Update(_entities.FrameCounter, _player.Position);
            FailIf(target.IsDead != (native[0xd080] == 0),
                "Armos Bomb JUST_HIT must return before the next eligible NO_HEALTH/death dispatch.");
        });
        FailIf(!target.IsDead || _entities.Entities<EnemyDeathPuffEffect>().Count != 1,
            "Armos death must create one PART_ENEMY_DESTROYED $02 after its pending hit clears.");
    }
}
