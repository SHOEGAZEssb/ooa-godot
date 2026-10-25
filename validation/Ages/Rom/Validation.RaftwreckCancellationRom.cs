using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRaftwreckCancellationRom()
    {
        foreach(int stage in new[]{0,1,2,3,4})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            CompanionRuntimeState.Begin(_runtimeState,0x13,0xa8,new(96,118),3);
            LoadValidationRoom(1,0xa8);_inventory.SetScriptedEquippedItems(0,0);
            var owner=_roomEvents.Get<RaftwreckEvent>();
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            var raft=_entities.Entities<RaftRoomEntity>().Single();
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            if(stage==0)_runtimeState.SetWramByte(0xcd00,0);
            int updates=0;
            bool Reached() => stage switch
            {
                0=>updates>=3,
                1=>owner.CenterCounter is >0 and <0x20,
                2=>runner.CurrentCommand?.Source.CommandIndex==4&&runner.CurrentCommandUpdates>2,
                3=>owner.FlashPhase==1&&owner.FlashFrame>=3,
                4=>runner.CurrentCommand?.Source.CommandIndex==41&&runner.CurrentCommandUpdates>2,
                _=>false
            };
            while(!Reached()&&owner.HasState&&updates<1500)
                StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    updates++;
                    if(stage!=0&&updates==1)_runtimeState.SetWramByte(0xcd00,0);
                });
            FailIf(!Reached()||!_player.CutsceneControlled||_saveData.HasRoomFlag(1,0xa8,0x40),
                $"Raftwreck cancellation stage{stage} did not reach its live pre-completion boundary.");
            owner.Cancel();
            Vector2 position=owner.PrecisePosition;
            for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
            StepGameplayUpdates(5,Vector2.Zero,batched:batched);
            FailIf(owner.HasState||owner.BlocksGameplay||_player.CutsceneControlled||!raft.LinkRiding||!_player.RaftRideActive||
                owner.PrecisePosition!=position||_saveData.HasRoomFlag(1,0xa8,0x40)||_transitions.IsTransitioning||
                Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i))||
                _entities.Entities<NpcCharacter>().Any(actor=>actor.Active&&actor.Name.ToString().StartsWith("Raftwreck",StringComparison.Ordinal)),
                $"Raftwreck cancellation stage{stage} must retire script/children and release control while retaining the live raft.");
            // An unfinished room still admits the original controller again.
            LoadValidationRoom(1,0xa8);
            FailIf(!owner.HasState||!_player.CutsceneControlled,
                "An unfinished raftwreck must restart on re-entry after cancellation.");
            owner.Cancel();
        }
    }
}
