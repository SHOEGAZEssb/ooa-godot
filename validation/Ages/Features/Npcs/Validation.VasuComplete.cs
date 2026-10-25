using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVasuComplete()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(new VasuShopDatabase().GlobalObtainedRingBox);
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(0);
            LoadValidationRoom(2, 0xee);
            VasuShopEvent shop = _roomEvents.Get<VasuShopEvent>();
            NpcCharacter vasu = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Vasu, SubId: 0 });
            _player.WarpTo(new Vector2(80, 88));
            FailIf(_rooms.CurrentRoom.IsSolid(_player.Position),
                "Vasu $89:$00 floor approach began inside room $2:ee collision geometry.");
            for (int update = 0; update < 24 && _player.Position.Y > 67; update++)
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"], batched);
            FailIf(_rooms.CurrentRoom.IsSolid(_player.Position) ||
                _entities.FindTalkTarget(_player) != vasu,
                $"Vasu $89:$00 could not be approached across the room $2:ee counter (Link {_player.Position}).");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.VasuNormalMenu || !_dialogue.IsOpen,
                "Vasu $89:$00 real A-button dispatch did not open TX_3003.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.VasuNormalMenuActive || !_ringMenu.IsActive,
                "Vasu $89:$00 ring-list choice did not hand off to MENU_RING_LIST.");
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!_ringMenuScreen.Visible || !_gameplayPause.IsLeased,
                "Vasu $89:$00 ring list did not hold the shared modal pause.");
            StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
            StepGameplayUpdates(36, Vector2.Zero, batched: batched);
            FailIf(_ringMenu.IsActive || shop.Stage != EventStage.VasuFinalText ||
                !_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("do nothing", StringComparison.OrdinalIgnoreCase),
                $"Vasu $89:$00 ring-list close did not return to TX_3008 (stage {shop.Stage}).");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.HasState || _gameplayPause.IsLeased,
                "Vasu $89:$00 did not release its conversation after the ring list.");

            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.VasuNormalMenu,
                "Vasu $89:$00 could not be spoken to again after menu completion.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!_ringMenu.IsActive,
                "Vasu $89:$00 repeat ring list did not acquire the menu.");
            shop.Cancel();
            FailIf(shop.HasState || _ringMenu.IsActive || _gameplayPause.IsLeased,
                "Canceling Vasu $89:$00 during MENU_RING_LIST leaked the menu pause or callback.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(shop.HasState || _ringMenu.IsActive,
                "Canceled Vasu $89:$00 ring list resumed after its owner retired.");
        }
    }
}
