using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormSwordWrite()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var pending = (List<RoomEntitySpawn>)typeof(RoomEntityManager).GetField("_pendingSpawns", flags)!.GetValue(_entities)!;
        var process = typeof(RoomEntityManager).GetMethod("ProcessSpawns", flags)!;
        var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Moldorm sword-part fixture must use the actual entrance approach.");
            _player.SetBraceletLiftCollisionsDisabled(true);
            FailIf(!_entities.TrySpawnEnemy(0x4f, 0, new Vector2(120, 80), "Moldorm PART_ENEMY_SWORD write", out string error), error);
            Step(3);
            var head = _entities.Entities<MoldormCharacter>().Single();
            _entities.EntityAdapters<MoldormRoomEntity>().Single(owner => owner.Node == head)
                .ApplySwordHit(head.CollisionBounds, head.Position, 0x7f, EnemyKnockbackStrength.Low, pending);
            for (int i = 0; head.KnockbackCounter > 1 && i < 40; i++) Step();
            FailIf(head.IsDead || head.KnockbackCounter != 1, "Moldorm sword-part fixture missed the final recoil update.");
            pending.Add(new KeeseFireSpawn(new Vector2(72, 80), 0));
            pending.Add(new KeeseFireSpawn(new Vector2(88, 80), 0));
            process.Invoke(_entities, [null]);
            FailIf(!_entities.TrySpawnEnemy(0x49, 0, new Vector2(88, 56), "Native Sword Stalfos blade allocation", out error), error);
            Step();
            var fighter = _entities.Entities<SwordEnemyCharacter>().Single();
            var blade = slots.Keys.OfType<EnemySwordRoomEntity>().Single();
            FailIf(slots[blade] != 2 || head.Tail1Slot != 2 || head.IsDead || head.KnockbackCounter != 0 || !blade.CollisionEnabled,
                "The sword enemy's native initialization must allocate its blade into the Moldorm tail's PART page before death.");
            blade.SetLinkSwordState(SwordActionState.Swing, 1);
            FailIf(!blade.ApplySwordHit(blade.CollisionBounds, blade.Node.Position + Vector2.Down * 16,
                2, EnemyKnockbackStrength.Low, pending) || blade.InvincibilityCounter != -11 || blade.KnockbackCounter != 9 || blade.KnockbackAngle != 0,
                "PART_ENEMY_SWORD collision must retain ENEMYDMG_$4c raw invincibility $f5, recoil9 and upward angle0.");
            Step();
            FailIf(_entities.Entities<MoldormCharacter>().Count != 0 || blade.Finished ||
                blade.InvincibilityCounter != -10 || fighter.InvincibilityCounter != -10 ||
                blade.KnockbackCounter != 9 || fighter.KnockbackCounter != 9 || fighter.KnockbackAngle != 0 ||
                blade.CollisionEnabled != fighter.SwordBlocking,
                "US zero health must preserve the blade, advance its invincibility before transfer, and run normal parent collision gates.");
            Step();
            FailIf(fighter.KnockbackCounter != 9 || blade.KnockbackCounter != 9 || blade.InvincibilityCounter != -9,
                "A zero-health blade must restore its unchanged recoil9 after the next ENEMY update decrements the parent's copy.");
            // A live parent's existing invincibility wins over the part copy.
            fighter.InvincibilityCounter = 5;
            Step();
            FailIf(fighter.InvincibilityCounter != 4 || blade.InvincibilityCounter != -8 || fighter.KnockbackCounter != 9,
                "partCode1d must preserve nonzero parent invincibility while always copying both recoil bytes.");
            Step(16);
            FailIf(blade.Finished || blade.InvincibilityCounter != 0 || fighter.KnockbackCounter != 9 || blade.KnockbackCounter != 9,
                "The zero-health blade's status branch must persist beyond invincibility expiry, including a zero invincibility copy.");
            LoadValidationRoom(4, 0x91);
            FailIf(slots.Keys.OfType<EnemySwordRoomEntity>().Any(), "Room replacement must release the affected native blade slot.");
        }
    }
}
