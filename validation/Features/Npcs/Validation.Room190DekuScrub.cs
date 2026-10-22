using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom190DekuScrub()
    {
        NpcRecord record = new NpcDatabase().GetRoomNpcs(1, 0x90).Single(n =>
            n.Id == 0xd6 && n.SubId == 0);
        var codec = new LinkedGameNpcDatabase();
        LinkedGameNpcDatabaseRecord secret = codec.Get(1, 0x90, 0xd6, 0);
        var animations = new DekuScrubDatabase();
        FailIf(record is not { Y: 0x18, X: 0x38, TextId: 0x4d23,
                DefaultAnimation: 0, CanFace: false,
                Implementation: NpcImplementationClassification.SpecializedNative } ||
            secret is not { SecretIndex: 7, ShortSecretIndex: 0x27,
                BeganFlag: 0x57, HasExtraText: false,
                OfferTextId: 0x4d23, RefusalTextId: 0x4d24,
                SecretTextId: 0x4d26, FinalTextId: 0x4d27 } ||
            !secret.OfferMessage.Contains("♪Love, love", StringComparison.Ordinal) ||
            !secret.SecretMessage.Contains("\\secret1\\stop", StringComparison.Ordinal) ||
            !PlainWords(secret.FinalMessage).Contains("Sunken City in Holodrum", StringComparison.Ordinal) ||
            animations.Animation(0) != record.DownAnimation ||
            animations.Animation(1) == animations.Animation(3),
            "Room 1:90 Deku Scrub $d6:$00 lost source pose, TX_4d23-TX_4d27 controls, or animation $00/$01/$03 data.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            LoadValidationRoom(1, 0x90);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == 0xd6 && n.Record.SubId == 0);
            FailIf(npc.Active || npc.Visible || _entities.BlocksLink(npc.Position),
                "Room 1:90 Deku Scrub $d6:$00 survived the unlinked stubScript deletion.");

            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x12);
            _saveData.WriteWramByte(WramAddress.wShortSecretIndex, 0);
            _saveData.SetGlobalFlag(0x57, false);
            LoadValidationRoom(1, 0x90);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == 0xd6 && n.Record.SubId == 0);
            FailIf(!npc.Active || !npc.Visible || npc.Position != new Vector2(0x38, 0x18) ||
                npc.CurrentAnimationOpaquePixels == 0 || !_entities.BlocksLink(npc.Position),
                "Room 1:90 Deku Scrub $d6:$00 lost linked-only admission, sprite, or collision.");

            // Exact native distance boundary: the source compares the sum of
            // high-byte X/Y differences with $20, excluding equality.
            Vector2 boundary = FindOpenPositionAtManhattanDistance(npc.Position, 0x20);
            _player.WarpTo(boundary);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            ulong hiddenPixels = npc.CurrentAnimationPixelHash;
            FailIf(hiddenPixels == 0,
                "Room 1:90 Deku Scrub $d6:$00 did not retain animation $00 at distance $20.");
            Vector2 inside = FindOpenPositionAtManhattanDistance(npc.Position, 0x1f);
            _player.WarpTo(inside);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            ulong shownPixels = npc.CurrentAnimationPixelHash;
            FailIf(shownPixels == hiddenPixels,
                "Room 1:90 Deku Scrub $d6:$00 did not select animation $01 inside $20.");
            _player.WarpTo(boundary);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(npc.CurrentAnimationPixelHash == shownPixels,
                "Room 1:90 Deku Scrub $d6:$00 did not select animation $03 at distance $20.");

            ApproachLibraryActor(npc, batched,
                counterpartReachable: () => _entities.FindTalkTarget(_player) == npc);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive || !_player.CutsceneControlled ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Want to hear the rest?", StringComparison.Ordinal),
                "Room 1:90 Deku Scrub $d6:$00 was unreachable or did not show TX_4d23.");
            int animationCounter = npc.CurrentAnimationCounter;
            bool animatedDuringText = false;
            StepGameplayUpdates(8, Vector2.Zero, batched: batched,
                afterUpdate: () => animatedDuringText |=
                    npc.CurrentAnimationCounter != animationCounter);
            FailIf(!animatedDuringText || !_dialogue.ChoiceActive ||
                !_player.CutsceneControlled,
                "Room 1:90 Deku Scrub $d6:$00 lost its always-update animation while TX_4d23 was open.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x57),
                "Room 1:90 Deku Scrub $d6:$00 refusal finished before wait 20.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Then be gone!" ||
                _saveData.HasGlobalFlag(0x57),
                "Room 1:90 Deku Scrub $d6:$00 refusal did not show TX_4d24 without flag $57.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);

            ApproachLibraryActor(npc, batched,
                counterpartReachable: () => _entities.FindTalkTarget(_player) == npc);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x57) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0,
                "Room 1:90 Deku Scrub $d6:$00 generated the secret before wait 20.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            // bank3.s packing, checksum and XOR for GameID $1234, index $27:
            // symbol values $0b,$29,$13,$1e,$21.
            byte[] sourceValues = [0x0b, 0x29, 0x13, 0x1e, 0x21];
            string sourceSecret = DialogueBox.PlainText(string.Concat(
                sourceValues.Select(value => codec.SecretSymbols[value])));
            FailIf(!_saveData.HasGlobalFlag(0x57) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x27 ||
                !_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(sourceSecret, StringComparison.Ordinal) ||
                !codec.GenerateSecretValues(0x27, _saveData).SequenceEqual(sourceValues),
                "Room 1:90 Deku Scrub $d6:$00 did not show source secret $27 or set flag $57.");
            string repeated = _dialogue.CurrentMessage;
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(_dialogue.CurrentMessage != repeated || !_dialogue.ChoiceActive,
                "Room 1:90 Deku Scrub $d6:$00 No did not repeat TX_4d26.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("pay him a visit!", StringComparison.Ordinal) ||
                _dialogue.ChoiceActive,
                "Room 1:90 Deku Scrub $d6:$00 Yes did not show TX_4d27.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x57),
                "Room 1:90 Deku Scrub $d6:$00 did not release Link or retain flag $57.");
            ApproachLibraryActor(npc, batched,
                counterpartReachable: () => _entities.FindTalkTarget(_player) == npc);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Want to hear the rest?", StringComparison.Ordinal),
                "Room 1:90 Deku Scrub $d6:$00 second A press did not repeat TX_4d23.");
            LoadValidationRoom(1, 0x91);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x57),
                "Room 1:90 Deku Scrub $d6:$00 cancellation retained input or cleared flag $57.");
        }
        GD.Print("Validated room 1:90 Deku Scrub $d6:$00 linked admission, native $20 animation boundary, collision approach, TX_4d23-TX_4d27, secret $27, repeat, cancellation and split/batched updates.");
    }

    private Vector2 FindOpenPositionAtManhattanDistance(Vector2 origin, int distance)
    {
        for (int y = 8; y < _currentRoom.Height - 8; y++)
        for (int x = 8; x < _currentRoom.Width - 8; x++)
        {
            Vector2 position = new(x, y);
            if (Math.Abs(x - origin.X) + Math.Abs(y - origin.Y) != distance ||
                _collision.Collides(position) || _entities.BlocksLink(position) ||
                _currentRoom.GetTerrainInfo(position).Collision != 0)
                continue;
            return position;
        }
        throw new InvalidOperationException(
            $"Room 1:90 has no open approach point at Deku Scrub distance ${distance:x2}.");
    }
}
