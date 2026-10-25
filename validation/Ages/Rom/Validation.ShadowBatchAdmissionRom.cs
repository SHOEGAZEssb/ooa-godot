using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CompareShadowBatchAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1, 2, 3, 4 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x4a);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(16, 112)); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<IRoomEntity>().Single(actor => actor.Node is ShadowHagBoss);
            var boss = (ShadowHagBoss)parent.Node; F1Retain(parent);
            F1Set(boss, "_initialized", true); F1Set(boss, "_introActive", false);
            F1Set(boss, "_state", ShadowHagState.GroundEyes); F1Set(boss, "_counter1", 0); F1Set(boss, "_counter2", 0);
            boss.Position = new(80, 72);
            var native = F1Native(0x7a, 0, 9, boss.Position); F1Fill(free, native);
            void Compare()
            {
                F1Enemy(native);
                int count = _entities.EntityAdapters<ShadowHagShadowRoomEntity>().Count();
                FailIf(count != F1Count(native, 0x41) || count != (boss.State == ShadowHagState.GroundEyes ? 0 : 4),
                    "Shadow Hag must admit all four shadows atomically or remain in its source waiting state.");
                FailIf((boss.State == ShadowHagState.GroundEyes ? 9 : 0x0a) != native[0xd084] || boss.Counter1 != native[0xd086] || boss.Counter2 != native[0xd087],
                    "Failed four-PART preflight must preserve Shadow Hag's state and return count.");
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (free < 4)
            {
                // Release only enough slots to reach the original four-slot
                // boundary; a partial group must never have been constructed.
                for (int slot = free; slot < 4; slot++)
                {
                    _entities.RetireEntity(_entities.ResolvePartSlot(slot)!);
                    for (int offset = 0; offset < 64; offset++) native[0xd0c0 + slot * 256 + offset] = 0;
                }
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            }
            FailIf(_entities.EntityAdapters<ShadowHagShadowRoomEntity>().Count() != 4, "Four newly free slots must unblock the next source preflight.");
            _entities.Clear();
        }
    }
}
