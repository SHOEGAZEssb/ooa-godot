using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTokayTheftCancellation()
    {
        foreach(int stage in new[]{0,1,2,3})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0xaa);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,88));
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            var owner=_roomEvents.Get<TokayTheftEvent>();
            int updates=0;
            bool Reached()=>stage switch
            {
                0=>true,1=>_player.TopDownAirborne,
                2=>owner.Thieves.Any(thief=>thief.Stage==3),
                3=>owner.Thieves.Any(thief=>thief.Stage==6),_=>false
            };
            while(!Reached()&&owner.HasState&&updates<1000)
                StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:()=>updates++);
            FailIf(!Reached()||!owner.HasState||!_player.CutsceneControlled,
                $"Tokay cancellation boundary{stage} was not reached.");
            var states=owner.Thieves.ToArray();
            owner.Cancel();
            var positions=states.Select(thief=>thief.Position).ToArray();
            StepGameplayUpdates(3,Vector2.Zero,batched:batched);
            FailIf(owner.HasState||owner.MenusDisabled||_player.CutsceneControlled||_player.TopDownAirborne||
                owner.AccessoryCount!=0||_runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)!=0||
                _saveData.HasRoomFlag(1,0xaa,0x40)||states.Where((thief,index)=>thief.Position!=positions[index]).Any()||
                states.Any(thief=>thief.Accessory?.Active==true),
                $"Tokay cancellation boundary{stage} must stop its runners and shared Link air state without completing the event.");
            LoadValidationRoom(0,0x11);LoadValidationRoom(1,0xaa);
            FailIf(!owner.HasState||owner.Thieves.Count!=5||owner.ScriptCounter!=0,
                "Unfinished Tokay theft must restart with five fresh, unstarted scripts on re-entry.");
            owner.Cancel();
        }
    }
}
