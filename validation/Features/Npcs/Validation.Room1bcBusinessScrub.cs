using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom1bcBusinessScrub()
    {
        // businessScrub.s:@sellingShield uses subid $00 plus max(0,shield-1)
        // for the $50/$100/$150 shield offers and the $ce:$80 bush child.
        foreach (bool batched in new[] { false, true })
        foreach (int level in new[] { 0, 1, 2, 3 })
        foreach (int branch in new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation();
            if (level != 0)
            {
                _inventory.GiveTreasure(TreasureId.Shield, level);
                if (branch != 3)
                    _inventory.LoseTreasure(TreasureId.Shield);
            }
            else if (branch == 3)
                _inventory.GiveTreasure(TreasureId.Shield, 1);
            int effective = level == 0 ? 0 : level - 1;
            int price = new[] { 50, 100, 150 }[effective];
            int wallet = branch == 2 ? price - 1 : 500;
            _inventory.AddRupees(wallet - _inventory.Rupees);
            LoadValidationRoom(1, 0xbc);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var scrub = _entities.EntityAdapters<BusinessScrubRoomEntity>().Single();
            FailIf(scrub.Npc.Record is not { Group: 1, Room: 0xbc, Id: InteractionId.BusinessScrub, SubId: 0 } ||
                scrub.Npc.Position != new Vector2(0x48, 0x18) ||
                scrub.Offer.Price != price || scrub.Offer.Parameter != effective + 1 ||
                scrub.Offer.EffectiveSubId != effective ||
                scrub.BushSlot is null ||
                _entities.InteractionSlot(scrub.Npc) == _entities.InteractionSlot(scrub.BushSlot.Node),
                $"Room 1:bc $ce:$00 lost source placement, ${effective:x2} offer, or $ce:$80 child allocation.");
            ApproachLibraryActor(scrub.Npc, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    !_dialogue.CurrentMessage.Contains(price.ToString(), StringComparison.Ordinal),
                    $"Room 1:bc $ce:$00 shield offer ${effective:x2} was unreachable on talk {repeat}.");
                _dialogue.SubmitChoiceForValidation(branch == 1 ? 1 : 0);
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                int expectedWallet = branch == 0 && repeat == 0 ? wallet - price : wallet;
                if (branch == 0 && repeat == 1)
                    expectedWallet -= price;
                string resultWords = branch == 0
                    ? repeat == 0 ? "Thank you!" : "You already"
                    : branch == 1 ? "Then be gone!"
                    : branch == 2 ? "enough Rupees!" : "You already";
                FailIf(_inventory.Rupees != expectedWallet ||
                    _inventory.HasTreasure(TreasureId.Shield) != (branch is 0 or 3) ||
                    !_dialogue.IsOpen ||
                    !_dialogue.CurrentMessage.Contains(resultWords, StringComparison.Ordinal),
                    $"Room 1:bc $ce:$00 branch {branch} repeat {repeat} lost its source result or changed the wrong wallet/shield state: wallet {_inventory.Rupees}/{expectedWallet}, shield {_inventory.HasTreasure(TreasureId.Shield)}, dialogue {_dialogue.IsOpen}, text '{_dialogue.CurrentMessage}', expected '{resultWords}'.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.IsOpen || !_roomEvents.Get<BusinessScrubEvent>().HasState,
                "Room 1:bc $ce:$00 failed to reopen its prompt before cancellation.");
            LoadValidationRoom(1, 0xbd);
            FailIf(_roomEvents.Get<BusinessScrubEvent>().HasState,
                "Leaving room 1:bc retained Business Scrub purchase state.");
        }
        GD.Print("Validated room 1:bc $ce:$00 source shield offers, bush child, real geometry, all purchase branches, repeat talks and room-exit cleanup.");
    }
}
