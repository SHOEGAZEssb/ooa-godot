using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateFairyPendingCutsceneRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _saveData.WriteWramByte(0xc6a2, 1);
            _runtimeState.SetWramByte(0xcfd0, 1); _runtimeState.SetWramByte(0xcfd1, 7);
            LoadValidationRoom(0, 0x82);
            var parent = System.Linq.Enumerable.Single(_entities.EntityAdapters<FairyHidingMinigameRoomEntity>());
            F1Retain(parent);
            _player.WarpTo(new(80, 112));
            FailIf(_collision.Collides(_player.Position), "Pending-cutscene fixture requires real forest floor.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, 112);
            native.InitializeLinkGameplay();
            native[0xc2ef] = 1; native[0xcfd0] = 1; native[0xcfd1] = 7;
            native[0xd240] = 1; native[0xd241] = 0x6c;
            // Real $84:$02 effects fill all remaining slots. State0 must keep
            // publishing its request until their native lifetimes free space.
            AddFairySlotSparkles(native, 13);
            int updates = 0, selections = 0, completions = 0, generation = 0, textHold = 0;
            void Compare()
            {
                bool normal = native[0xc2ef] == 1;
                if (!normal)
                {
                    native.AdvancePaletteThread();
                    native.AdvanceFairyHidingCutscene();
                }
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                if (normal)
                {
                    native.SelectTriggeredCutscene();
                    if (native[0xc2ef] == 0x13) selections++;
                }
                else if (native[0xc2ef] == 1) completions++;
                string boundary = $"pending fairy update{++updates}, batch={batched}";
                CompareFairyInteractionSlotsRom(native, boundary);
                foreach (int address in new[] { 0xc2ef, 0xcc03, 0xcc04, 0xcbb6, 0xcc02, 0xcc8a, 0xcfd0, 0xcfd1, 0xcfd2 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"{boundary}: WRAM${address:x4} differs, runtime=${_runtimeState.ReadWramByte(address):x2}, native=${native[address]:x2}.");
                foreach (int flag in new[] { 0x0e, 0x2b })
                    FailIf(_saveData.HasGlobalFlag(flag) != ((native[0xc6d0 + flag / 8] & (1 << (flag & 7))) != 0),
                        $"{boundary}: global flag${flag:x2} differs.");
                FailIf(_dialogue.IsOpen != (native[0xcba0] != 0), $"{boundary}: native textbox lifetime differs.");
                if (updates == 2)
                    FailIf(parent.Finished || parent.State != 0 || native[0xcc04] != 0x13 || selections != 1,
                        "A full pool must retain $6c state0 and its second request while cutscene $13 is already running.");
                if (completions == 1 && !normal && native[0xc2ef] == 1)
                    FailIf(native[0xcc04] != 0x13 || native[0xcc03] != 5,
                        "Finishing cutscene $13 must leave its queued request for the next ordinary caller.");
                // The original text thread is outside this fixture. Explicitly
                // hold each source textbox for three object passes, then close.
                if (native.TextGeneration != generation) { generation = native.TextGeneration; textHold = 3; }
                if (textHold != 0 && --textHold == 0) { _dialogue.Close(); native[0xcba0] = 0; }
            }
            while (completions < 2 && updates < 800)
                StepGameplayUpdates(batched ? 3 : 1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(selections != 2 || completions != 2 || generation != 4 || !parent.Finished || native[0xcc04] != 0,
                "Partial state0 admission must preserve the one queued request, execute both native completions and then return without a third request.");
            _roomEvents.Get<FairiesWoodsEvent>().Cancel();
        }
    }
}
