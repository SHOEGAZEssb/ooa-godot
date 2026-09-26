using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownPortal()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var counter=typeof(MinibossPortalRoomEntity).GetField("_counter",flags)!;
        foreach(bool batch in new[]{false,true})
        foreach(int source in new[]{0xbb,0xb4})
        {
            void Step(int count=1,Vector2 movement=default) =>
                StepGameplayUpdates(count,movement,batched:batch);
            _saveData.SetRoomFlag(4,0xb4,OracleSaveData.RoomFlag80,true);
            LoadValidationRoom(4,source);
            _player.WarpTo(new(120,120));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown portal approach must start on real floor.");
            Step();
            var portal=((IEnumerable<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!
                .GetValue(_entities)!).OfType<MinibossPortalRoomEntity>().Single();
            Warp? warp=null;
            void Capture(Warp request) => warp=request;
            _entities.RoomWarpRequested+=Capture;
            _sound.ClearPlayRequestAudit();
            try
            {
                for(int i=0;!_player.CutsceneControlled && i<48;i++) Step(movement:Vector2.Up);
                FailIf(!_player.IsCutsceneControlOwner(portal) || _player.Position!=new Vector2(120,88) ||
                    (int)counter.GetValue(portal)! != 0x30 || warp.HasValue ||
                    _sound.PlayRequestsFor(SoundId.SndTeleport)!=1,
                    "Fresh floor contact must pin Link to Crown's portal, acquire control and load counter$30 once.");
                // Source spins on global frame multiples of4. Let state08
                // initialize first, then four updates must turn exactly once.
                Step();
                Vector2I facing=_player.FacingVector;
                Step(4,Vector2.Right);
                Vector2I expected=facing==Vector2I.Up ? Vector2I.Right : facing==Vector2I.Right ?
                    Vector2I.Down : facing==Vector2I.Down ? Vector2I.Left : Vector2I.Up;
                FailIf(_player.FacingVector!=expected || _player.Position!=new Vector2(120,88),
                    "Portal spin must follow global four-update direction timing and ignore movement input.");
                Step(42);
                FailIf(warp.HasValue || (int)counter.GetValue(portal)! != 1,
                    "Crown portal must retain counter1 after47 spin updates.");
                Step();
                int destination=source==0xbb ? 0xb4 : 0xbb;
                FailIf(warp is not {SourceGroup:4,SourcePosition:0x57,SourceTransition:WarpSourceTransition.FadeOut,
                        DestinationGroup:4,DestinationPosition:0x57,DestinationParameter:0,
                        DestinationTransition:WarpDestinationTransition.Basic,DirectFadeOut:true} ||
                    warp.Value.SourceRoom!=source || warp.Value.DestinationRoom!=destination,
                    "Crown portal must use the source $b4/$bb pair and direct-fade $57 exit on update48.");
                for(int i=0;IsTransitioning && i<180;i++) Step();
                FailIf(IsTransitioning || _currentRoom.Id!=destination || _rooms.ActiveGroup!=4 ||
                    _player.Position!=new Vector2(120,88) || _player.CutsceneControlled,
                    "Crown portal arrival must release control at the paired portal.");
                warp=null;
                Step(8);
                FailIf(warp.HasValue || _player.CutsceneControlled ||
                    _sound.PlayRequestsFor(SoundId.SndTeleport)!=1,
                    "Arrival overlap must not immediately retrigger the destination portal.");
                Step(16,Vector2.Down);
                for(int i=0;!_player.CutsceneControlled && i<48;i++) Step(movement:Vector2.Up);
                FailIf(!_player.CutsceneControlled || _sound.PlayRequestsFor(SoundId.SndTeleport)!=2,
                    "Leaving and approaching the destination portal again must permit a fresh use.");
                // Cancellation removes the owning entity while it holds Link.
                _entities.Clear();
                FailIf(_player.CutsceneControlled,"Removing a spinning portal must release its player control.");
                LoadValidationRoom(0,0x60);
            }
            finally { _entities.RoomWarpRequested-=Capture; }
        }
    }
}
