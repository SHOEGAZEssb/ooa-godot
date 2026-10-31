using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateForestRescueHandoffRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.AssignAnimalCompanion(id);
            _saveData.SetGlobalFlag(0x22); _saveData.SetGlobalFlag(0x42);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetGlobalFlag(0x24, false);
            _saveData.WriteWramByte(0xc646 + id - 0x0b, 0x60); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x81); _player.WarpTo(new(80, 112), recordSafe: false);
            FailIf(_collision.Collides(_player.Position), "Forest rescue must approach its companion through actual room $0:$81 floor.");
            var parent = _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Single();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 80, 112)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false,
                    HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            int updates = 0, generation = 0, textHold = 0, held = 0, pressed = 0;
            bool requested = false;
            var texts = new List<int>();
            void Compare()
            {
                if (requested) return;
                native.UpdateGameplay(pressed, held, 0xff, _entities.FrameCounter); pressed = 0; updates++;
                string context = $"Forest rescue ${id:x2}, linked={linked}, batch={batched}, update{updates}";
                CompareFairyInteractionSlotsRom(native, context);
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                    0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd110, 0xd114, 0xd115,
                    0xd11a, 0xd120, 0xd121, 0xd124, 0xd126, 0xd127, 0xd130, 0xd138, 0xd139, 0xd13d, 0xd13f,
                    0xcc02, 0xcc8a, 0xcc2c, 0xcc6e, 0xcc91, 0xcc24, 0xcc25, 0xcc26, 0xcc27, 0xcc28, 0xcfd2 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"{context}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                FailIf(_player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                    _dialogue.IsOpen != (native[0xcba0] != 0) ||
                    _saveData.ReadWramByte(0xc6d4) != native[0xc6d4] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    context + ": Link, text, progression or ordered RNG differs.");
                if (native.TextGeneration != generation)
                {
                    generation = native.TextGeneration; textHold = 3;
                    int text = native.Word(0xcba2) - 0x400;
                    // The native text thread expands these companion calls.
                    text = text == 0x1131 ? 0x1133 + (id - 0x0b) * 7
                        : text == 0x1132 ? (linked ? 0x1135 : 0x1134) + (id - 0x0b) * 7 : text;
                    texts.Add(text);
                    FailIf(_dialogue.SourceTextId != text, $"{context}: native TX_{text:x4} was replaced or delayed.");
                }
                // Explicit text-thread input; native object/script dispatch
                // retains every counter and carry boundary between requests.
                if (textHold != 0 && --textHold == 0) { _dialogue.Close(); native[0xcba0] = 0; }
                if (native[0xcc4b] == 0) return;
                requested = true;
                var warp = (Warp)typeof(RoomTransitionController).GetField("_pendingWarp", F1Hidden)!.GetValue(_transitions)!;
                FailIf(native[0xcc47] != 0x80 || native[0xcc48] != 0x63 || native[0xcc49] != 0 ||
                    native[0xcc4a] != 0x56 || native[0xcc4b] != 3 || !parent.Finished ||
                    warp.DestinationGroup != 0 || warp.DestinationRoom != 0x63 || warp.DestinationPosition != 0x56 ||
                    warp.DestinationTransition != 0 || !warp.DirectFadeOut || _transitions.PhaseName != "FadeOut",
                    "Forest rescue must retire $71:$09 and publish the native $80/$63/$00/$56/$03 white-fade handoff after objects.");
                native.ApplyRequestedWarp();
                FailIf(native[0xc4ab] != 1 || native[0xc2ef] != 3,
                    "The native rescue warp must select its white palette thread and CUTSCENE $03.");
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            held = pressed = 0x40;
            StepGameplayUpdates(20, Vector2.Up, ["move_up"], ["move_up"], batched, Compare);
            held = pressed = 1;
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched, Compare);
            held = pressed = 0;
            while (!requested && updates < 700)
                StepGameplayUpdates(batched ? 4 : 1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            int shift = (id - 0x0b) * 7;
            FailIf(!requested || !texts.SequenceEqual(new[] { 0x1133 + shift, (linked ? 0x1135 : 0x1134) + shift, 0x112a }) ||
                !_saveData.HasGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest),
                "The reachable native forest rescue must finish its three source texts and saved-companion flag before warping.");
        }
    }
}
