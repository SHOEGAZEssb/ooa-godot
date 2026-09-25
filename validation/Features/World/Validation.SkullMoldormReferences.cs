using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormReferences()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Moldorm reference test must approach through D4's actual entrance floor.");
            _player.SetBraceletLiftCollisionsDisabled(true);
            FailIf(!_entities.TrySpawnEnemy(0x4f, 0, new Vector2(120, 80), "Moldorm native reference fixture", out string error), error);
            Step(3);
            var head = _entities.Entities<MoldormCharacter>().Single();
            var tail1 = head.Tail1!;
            var tail2 = head.Tail2!;
            FailIf(head.NativeSlot != 1 || head.Tail1Slot != 2 || head.Tail2Slot != 3 ||
                tail1.NativeSlot != 2 || tail1.ParentSlot != 1 || tail2.ParentSlot != 2 ||
                tail1.Parent != head || tail2.Parent != tail1,
                "Moldorm relatedObj1 and head var30/31 must retain the allocated ENEMY page indices.");

            // Isolate the source's recycled-slot edge case: delete the head,
            // then allocate another enemy into its page before the tail pass.
            // This fixture controls allocation timing; subsequent observations
            // use the ordinary application loop and actual enemy handlers.
            Vector2 replacementPosition = head.Position;
            FailIf(!head.TakeSwordHit(head.Position, 0x7f), "Moldorm head deletion fixture rejected its hit.");
            _entities.ApplySwordHit(new Rect2(-100, -100, 1, 1)); // Flush the completed owner without advancing tails.
            var reserved = (HashSet<int>)typeof(RoomEntityManager).GetField("_reservedEnemySlots", flags)!.GetValue(_entities)!;
            bool occupiedZero = reserved.Add(0);
            try
            {
                FailIf(!_entities.TrySpawnEnemy(0x34, 1, replacementPosition, "Reused Moldorm parent ENEMY page", out error), error);
            }
            finally { if (occupiedZero) reserved.Remove(0); }
            var replacement = _entities.Entities<ZolCharacter>().Single();
            FailIf(tail1.Parent != replacement || tail2.Parent != tail1,
                "Moldorm tail must follow the current occupant of its parent page, without checking the old object's identity or enemy ID.");
            Step(16);
            FailIf(tail1.IsDead || tail2.IsDead || _entities.Entities<MoldormTailCharacter>().Count != 2 ||
                tail1.Parent != replacement || _entities.RoomEnemyCount != 2,
                $"Reusing the parent ENEMY slot must keep both Moldorm tail objects and their independent counts alive: tails={_entities.Entities<MoldormTailCharacter>().Count}, count={_entities.RoomEnemyCount}, dead={tail1.IsDead}/{tail2.IsDead}, parent={tail1.Parent?.GetType().Name}.");
            FailIf(!replacement.TakeSwordHit(replacement.Position, 0x7f), "Replacement Zol death fixture rejected its hit.");
            Step(2);
            FailIf(_entities.Entities<MoldormTailCharacter>().Count != 0 || tail1.Parent is not null ||
                _entities.RoomEnemyCount != 0 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
                head.Tail1Slot != 2 || head.Tail2Slot != 3,
                "Clearing the reused parent page must delete the two tails, while the old head's raw tail page values remain unchanged.");
            Step(24);
            FailIf(_entities.RoomEnemyCount != 0, "Replacement Zol death puff retained the final room count.");
        }
    }
}
