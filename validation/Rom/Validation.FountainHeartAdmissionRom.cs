using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFountainHeartAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (var test in new (int Free, bool Release)[] { (0, false), (0, true), (1, false) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(2, 0xdf);
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.ApplyDamage(4); _player.WarpTo(new(80, 112));
            FailIf(_collision.Collides(_player.Position), "Fairy capacity regression must approach over the actual room floor.");
            var fairy = _entities.Entities<FountainFairyRoomEntity>().Single();
            StepGameplayUpdates(60, Vector2.Zero, batched: batched);
            for (int step = 0; step < 20 && fairy.State == 3; step++) StepGameplayUpdates(1, Vector2.Up, batched: batched);
            FailIf(fairy.State != 4 || !_dialogue.IsOpen || !_player.CutsceneControlled || _collision.Collides(_player.Position),
                "Fairy healing must begin through the real reachable approach and dialogue.");
            _dialogue.Close();
            var native = F1Native(0x38, 0, 0, fairy.Position); F1Enemy(native); native[0xd084] = 4;
            F1Fill(test.Free, native);
            int elapsed = 0;
            void Compare()
            {
                elapsed++;
                if (native[0xd080] != 0) F1Enemy(native);
                native[0xffb0] = (byte)_player.Position.Y; native[0xffb1] = (byte)_player.Position.X;
                native[0xcbe4] = (byte)_entities.ReadDisplayedHealth();
                // Initialized filler slots are declared no-ops; retain their
                // occupancy during allocation, then suppress only their dispatch.
                var fillers = F1Parts.Where(pair => pair.Key is ItemPhaseValidationEntity).Select(pair => pair.Value).ToArray();
                foreach (int slot in fillers) native[0xd0c0 + slot * 256] = 0;
                native.AdvanceParts(_entities.FrameCounter);
                foreach (int slot in fillers) native[0xd0c0 + slot * 256] = 1;
                FailIf(fairy.HeartCount != native[0xd0b1] || _entities.Entities<FountainFairyHeartRoomEntity>().Count != F1Count(native, 0x30),
                    $"Fairy must count only admitted PART$30 hearts through completion, free={test.Free}, release={test.Release}, update={elapsed}.");
            }
            StepGameplayUpdates(15, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (test.Release)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(0)!);
                for (int i = 0; i < 64; i++) native[0xd0c0 + i] = 0;
            }
            StepGameplayUpdates(360, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(fairy.HeartCount != 0 || _player.CutsceneControlled || native[0xd080] != 0,
                $"Fairy allocation completion: free={test.Free}, release={test.Release}, runtime={fairy.State}/{fairy.HeartCount}/control={_player.CutsceneControlled}, native={native[0xd080]}/{native[0xd084]}/{native[0xd0b1]}.");
            _entities.Clear();
        }
    }
}
