using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVasuHelpBooks()
    {
        // ringHelpBook.s:interactionCodee5 installs a $06 A-button radius,
        // but has no objectMarkSolidPosition or Link-separation call. The
        // two script loops begin at TX_3020 and TX_3019 respectively.
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(2, 0xee);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            VasuShopEvent shop = _roomEvents.Get<VasuShopEvent>();
            NpcCharacter[] books = _entities.Entities<NpcCharacter>()
                .Where(n => n.Record.Id == InteractionId.RingHelpBook)
                .OrderBy(n => n.Record.SubId).ToArray();
            FailIf(books.Length != 2 ||
                books[0].Record is not { SubId: 0, Y: 0x48, X: 0x28, Palette: 1 } ||
                books[1].Record is not { SubId: 1, Y: 0x48, X: 0x78, Palette: 2 } ||
                books.Any(n => !n.ScriptButtonSensitive || n.ObjectCollisionBounds.Size != new Vector2(12, 12) ||
                    _entities.BlocksLink(n.Position)),
                "Room 2:ee ring books $e5:$00/$01 lost source order, A radius, palette or nonblocking collision.");
            FailIf(!shop.Database.Text(0x3020).Contains("Ring Link\nBasics", StringComparison.Ordinal) ||
                !shop.Database.Text(0x3025).Contains("Pick a topic", StringComparison.Ordinal) ||
                !shop.Database.Text(0x3026).Contains("About Ring Links", StringComparison.Ordinal) ||
                !shop.Database.Text(0x303d).Contains("Ring Fortunes", StringComparison.Ordinal) ||
                !shop.Database.Text(0x3019).Contains("All About\nRing Secrets", StringComparison.Ordinal) ||
                !shop.Database.Text(0x301a).Contains("Red\nSnake", StringComparison.Ordinal),
                "Room 2:ee help books lost source TX_3020/$25/$26/$3d/$19/$1a content.");
            FailIf(_entities.Entities<NpcCharacter>().Where(n => n.Record.Id == InteractionId.Vasu && n.Record.SubId != 0)
                    .Any(n => _entities.BlocksLink(n.Position)),
                "vasu.s snake subids blocked Link despite using the priority-only tail.");

            NpcCharacter basics = books[0];
            ApproachLibraryActor(basics, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.BookBasicsInitial ||
                PlainWords(_dialogue.CurrentMessage) != PlainWords(shop.Database.Text(0x3020)),
                "Room 2:ee book $e5:$00 did not open TX_3020 from a reachable A-button point.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.BookBasicsTopic ||
                PlainWords(_dialogue.CurrentMessage) != PlainWords(shop.Database.Text(0x3025)),
                "Book $e5:$00 did not advance to TX_3025 topics.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.BookBasicsFortune ||
                PlainWords(_dialogue.CurrentMessage) != PlainWords(shop.Database.Text(0x303d)),
                "Book $e5:$00 did not select TX_303d Fortune instructions.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.BookBasicsTopic,
                "Book $e5:$00 did not loop to its topic menu.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.BookBasicsLink ||
                PlainWords(_dialogue.CurrentMessage) != PlainWords(shop.Database.Text(0x3026)),
                "Book $e5:$00 did not select TX_3026 Link instructions.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Book $e5:$00 did not return to its checkabutton loop.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.BookBasicsInitial,
                "Book $e5:$00 could not be read again after finishing.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Book $e5:$00 did not reset on Don't read.");

            NpcCharacter secrets = books[1];
            ApproachLibraryActor(secrets, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.BookSecretsInitial ||
                PlainWords(_dialogue.CurrentMessage) != PlainWords(shop.Database.Text(0x3019)),
                "Room 2:ee book $e5:$01 did not open TX_3019 from a reachable A-button point.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.BookSecretsText ||
                PlainWords(_dialogue.CurrentMessage) != PlainWords(shop.Database.Text(0x301a)),
                "Book $e5:$01 did not present TX_301a Red Snake instructions.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Book $e5:$01 did not return to its checkabutton loop.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.BookSecretsInitial,
                "Book $e5:$01 could not be read again after finishing.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Book $e5:$01 did not reset on Don't read.");
        }
        GD.Print("Validated room 2:ee $e5:$00/$01 nonblocking A radii, collision-valid approaches, both source topic loops and repeated reading.");
    }
}
