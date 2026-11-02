
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateForestFluteHandoffRom()
    {
        var commands = new CompanionForestDatabase().Commands(0x0a);
        FailIf(commands[18] is not CutsceneWriteMemoryCommand { Binding: "w1Companion.var03", Value: 0x01 } ||
            commands[20] is not CutsceneMemoryGateCommand { Binding: "wLinkObjectIndex", Value: 0xd1 },
            "scriptHelper.s:$0a must retain its native $d103 write and $cc2c==$d1 mount gate as data.");
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool linked in new[] { false, true })
        foreach (bool hadFlute in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _inventory.SetScriptedEquippedItems(0, 0);
            if (hadFlute) _inventory.GiveTreasure(TreasureId.Flute, id);
            else _inventory.AssignAnimalCompanion(id);
            _saveData.SetGlobalFlag(0x22); _saveData.SetGlobalFlag(0x42);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetGlobalFlag(0x24);
            _saveData.WriteWramByte(0xc646 + id - 0x0b, 0x60); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x63); _player.WarpTo(new(104.5f, 88.25f), recordSafe: false);
            FailIf(_collision.Collides(_player.Position), "The flute handoff must start on actual room $0:$63 floor.");
            var controller = _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Single();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 104, 88)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false,
                    HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.Word(0xd00a, 88 * 256 + 0x40); native.Word(0xd00c, 104 * 256 + 0x80);
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            _sound.ClearPlayRequestAudit();
            int updates = 0, generation = 0, textHold = 0;
            bool sawReward = false;
            var texts = new List<int>();
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++;
                string context = $"Forest flute ${id:x2}, linked={linked}, owned={hadFlute}, batch={batched}, update{updates}";
                CompareFairyInteractionSlotsRom(native, context);
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                    0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd110, 0xd114, 0xd115,
                    0xd11a, 0xd120, 0xd121, 0xd124, 0xd126, 0xd127, 0xd130, 0xd138, 0xd139, 0xd13d, 0xd13f,
                    0xcc02, 0xcc8a, 0xcc2c, 0xcc6e, 0xcc91, 0xcc24, 0xcc25, 0xcc26, 0xcc27, 0xcc28,
                    0xcc4f, 0xcc50, 0xcfd2 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"{context}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                FailIf(_dialogue.IsOpen != (native[0xcba0] != 0) ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    context + ": dialogue or ordered RNG differs.");
                FailIf(_player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                    _player.IsHoldingItemTwoHands != (native[0xd004] == 4 && native[0xd005] == 1),
                    context + ": Link position or state-$04 item-get pose differs.");
                FailIf(_saveData.ReadWramByte(0xc6b5) != native[0xc6b5] ||
                    _saveData.ReadWramByte(0xc646 + id - 0x0b) != native[0xc646 + id - 0x0b] ||
                    _saveData.ReadWramByte(0xc6d4) != native[0xc6d4] || _saveData.ReadWramByte(0xc6d5) != native[0xc6d5],
                    context + ": flute icon, companion state or forest progression bytes differ.");
                FailIf(_sound.PlayRequestsFor(SoundId.SndGetItem) != native.Sounds.Count(sound => sound == 0x4c),
                    context + ": flute grant emitted a different number of SND_GETITEM requests.");
                if (native.TextGeneration != generation)
                {
                    generation = native.TextGeneration; textHold = 3;
                    int text = native.Word(0xcba2) - 0x400;
                    // Native TX_1131/1132 expand companion-specific calls in
                    // the omitted text thread; the presenter resolves them now.
                    text = text == 0x1131 ? (linked ? 0x1137 : 0x1136) + (id - 0x0b) * 7
                        : text == 0x1132 ? 0x1139 + (id - 0x0b) * 7 : text;
                    texts.Add(text);
                    FailIf(_dialogue.SourceTextId != text, $"{context}: source TX_{text:x4} was replaced or delayed.");
                }
                if (controller.State == 3 && !controller.Finished)
                {
                    int at = 0xd040 + _entities.NativeInteractionSlot(controller) * 256;
                    var flute = (NpcCharacter)controller.PresentationNode!;
                    FailIf(flute.Visible != ((native[at + 0x1a] & 0x80) != 0), context + ": flute visibility differs.");
                    if (flute.Visible)
                    {
                        sawReward = true;
                        FailIf(flute.Position != new Vector2(native[at + 13], native[at + 11]) ||
                            flute.Record.Palette != native[at + 0x1c] || flute.CurrentAnimationOpaquePixels == 0 ||
                            native[at + 0x1a] != 0x80 ||
                            flute.ZIndex != ObjectDrawPriority.FixedHighPriorityZIndex,
                            context + ": flute position, palette, native priority or sprite is wrong.");
                    }
                }
                // This object-loop fixture holds each native text request for
                // three updates; text-thread drawing and dismissal are inputs.
                if (textHold != 0 && --textHold == 0) { _dialogue.Close(); native[0xcba0] = 0; }
            }
            while (!controller.Finished && updates < 1000)
                StepGameplayUpdates(batched ? 4 : 1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            int shift = (id - 0x0b) * 7;
            int[] expected = [0x112b, 0x112c, 0x112d, 0x112e, 0x112f,
                (linked ? 0x1137 : 0x1136) + shift, (hadFlute ? 0x0069 : 0x0038) + id - 0x0b, 0x1139 + shift];
            FailIf(!controller.Finished || !sawReward || !texts.SequenceEqual(expected) ||
                !_player.CompanionRideActive || !_saveData.HasGlobalFlag(AgesGlobalFlag.ForestUnscrambled),
                "The native flute continuation must reach all eight source texts, the item-get handoff and mount-gated completion.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: Compare);
        }
    }
}
