using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CheckRetailShopItemGetBoundary()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            var data=new LynnaShopDatabase();
            _saveData.SetLinkedGame(false);_saveData.SetGlobalFlag(data.GlobalCanBuyFlute,false);
            for(int repeat=0;repeat<2;repeat++)
            {
                LoadValidationRoom(2,0x5e);StepGameplayUpdates(1,Vector2.Zero,batched:batched);
                var shop=_roomEvents.Get<LynnaShopEvent>();
                var item=_entities.Entities<LynnaShopItem>().Single(stock=>stock.Record.SubId==1);
                var keeper=_entities.Entities<NpcCharacter>().Single(npc=>npc.Record.Id==0x46);
                _inventory.SetScriptedHealth(Math.Max(1,_inventory.MaxHealthQuarters-4));
                _statusBar.SynchronizeHealth();_inventory.AddRupees(999-_inventory.Rupees);
                _player.WarpTo(new(80,104));
                void Step(int count=1)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
                void Walk(Vector2 target)
                {
                    for(int axis=0;axis<2;axis++)
                    {
                        int limit=180;
                        while(Mathf.Abs(axis==0?_player.Position.X-target.X:_player.Position.Y-target.Y)>1)
                        {
                            if(--limit==0)throw new InvalidOperationException($"Retail shop approach blocked {_player.Position}->{target}, stage{shop.Stage}.");
                            Vector2 direction=axis==0?new(Mathf.Sign(target.X-_player.Position.X),0):new(0,Mathf.Sign(target.Y-_player.Position.Y));
                            string action=direction==Vector2.Left?"move_left":direction==Vector2.Right?"move_right":direction==Vector2.Up?"move_up":"move_down";
                            StepGameplayUpdates(1,direction,[action],[action],batched:batched);
                        }
                    }
                    Step();FailIf(_collision.Collides(_player.Position),"Retail shop approach must remain on actual collision-free floor.");
                }
                void PressA(){StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step();}
                Walk(new(80,58));Walk(new(item.ShelfPosition.X,58));_player.Face(Vector2I.Up);PressA();
                FailIf(!item.Held || !_player.IsCarryingObject,"Retail shop item must be lifted through its actual shelf geometry.");
                // The vertical$06 radius requires the ten-pixel A probe within16 pixels.
                Vector2I offset=new[]{Vector2I.Up,Vector2I.Right,Vector2I.Left,Vector2I.Down}
                    .First(direction=>!_collision.Collides(keeper.Position+(Vector2)direction*(direction.X==0?14:20)));
                Walk(new(80,58));Walk(keeper.Position+(Vector2)offset*(offset.X==0?14:20));_player.Face(-offset);PressA();
                FailIf(shop.Stage!=LynnaShopEventStage.PurchasePrompt || !_dialogue.IsOpen,
                    $"Retail keeper must offer the lifted product through the actual A-button interaction: Link{_player.Position}/{_player.FacingVector}, keeper{keeper.Position}, stage{shop.Stage}, held{item.Held}, text{_dialogue.CurrentMessage}.");
                _dialogue.SubmitChoiceForValidation(0);Step(3);
                FailIf(shop.Stage!=LynnaShopEventStage.ItemText || !item.Purchasing || !_player.IsHoldingItemTwoHands ||
                    _inventory.Rupees!=989 || _runtimeState.ReadField("wDisabledObjects")!=0x81 || _runtimeState.ReadField("wMenuDisabled")!=0x80,
                    "All shopItemState3 purchases must enter state04/$01 behind native object/menu masks, including ordinary retail stock.");
                _dialogue.Close();Step();
                FailIf(!item.Removed || !_player.IsHoldingItemTwoHands || _runtimeState.ReadField("wDisabledObjects")!=0 || _runtimeState.ReadField("wMenuDisabled")!=0,
                    "Retail stock must clear both masks after Link's held update, without releasing the pose on that same update.");
                Step();
                FailIf(!_player.NativeNormalStateForInteraction || _player.IsHoldingItemTwoHands || shop.HasState,
                    "Retail state04 must release on its next visit after the item clears its native mask.");
                Vector2 before=_player.PrecisePosition;StepGameplayUpdates(1,Vector2.Down,batched:batched);
                FailIf(_player.PrecisePosition==before,"Retail completion must release movement through the shared owner before repeating after re-entry.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
