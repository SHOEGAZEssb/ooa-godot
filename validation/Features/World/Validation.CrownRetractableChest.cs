using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownRetractableChest()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var set=(Action<int,bool>)typeof(RoomEntityManager).GetMethod("SetTrigger",flags)!.CreateDelegate(typeof(Action<int,bool>),_entities);
        void Triggers(int value) { for(int bit=0;bit<8;bit++) set(bit,(value&(1<<bit))!=0); }
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            _saveData.SetRoomFlag(4,0xbc,OracleSaveData.RoomFlagItem,false);
            LoadValidationRoom(4,0xbc);
            _player.WarpTo(new(24,24));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown chest fixture requires floor at $11.");
            var chest=_entities.Entities<RetractableTriggerChestRoomEntity>().Single();
            FailIf(chest.PackedPosition!=0x57 || _entities.InteractionSlot(chest)<2 ||
                !_entities.Entities<GroundButtonRoomEntity>().Select(b=>(b.SubId,b.PackedPosition)).SequenceEqual(new[]{(0x80,0x34),(0x81,0x3a),(0x82,0x74),(0x83,0x7a)}),
                "Crown must place INTERAC$21:$17 at$57 and four source-ordered reusable buttons.");
            // Isolate the consumer by holding text active and injecting its
            // shared trigger input; this does not assert button reachability.
            var text=_entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource=()=>true;
                _sound.ClearPlayRequestAudit();
                Triggers(0x1f); Step();
                FailIf(_currentRoom.Layout[0x57]!=0xa4,"Extra trigger bits must reject the exact$0f chest predicate.");
                Triggers(0x0f); Step();
                FailIf(_currentRoom.Layout[0x57]!=0xf1 || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=1,
                    "State0 chest must appear under text when triggers equal$0f.");
                Step(2);
                FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=1,"An existing chest must not replay its solve effect.");
                _currentRoom.SetUnderlyingMetatile(chest.Position,0xa0);
                Triggers(0); Step();
                FailIf(_currentRoom.Layout[0x57]!=0xa0 || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=1,
                    "Retraction must read the live room-layout buffer and omit the solve sound.");
                for(int i=0;i<31;i++) FailIf(!_rooms.TrySetTile(0x11,0xa0),"Queue-full fixture failed.");
                Triggers(0x0f); Step();
                FailIf(_currentRoom.Layout[0x57]!=0xa0 || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=2 || chest.Finished,
                    "Failed chest setTile must still request puff/solve and keep its state0 controller alive.");
                Step();
                FailIf(_currentRoom.Layout[0x57]!=0xf1 || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=3,
                    "After queue drain, the next exact-trigger dispatch must attempt chest creation again.");
                _saveData.SetRoomFlag(4,0xbc,OracleSaveData.RoomFlagItem,true);
                Triggers(0); Step();
                FailIf(!chest.Finished || _currentRoom.Layout[0x57]!=0xf1 || _entities.Entities<RetractableTriggerChestRoomEntity>().Count!=0,
                    "ROOMFLAG_ITEM must retire the controller before retraction.");
            }
            finally { _entities.TextActiveSource=text; }
            LoadValidationRoom(4,0xbc); Step();
            FailIf(_entities.Entities<RetractableTriggerChestRoomEntity>().Count!=0,"Collected-room re-entry must retire the chest controller.");
            _saveData.SetRoomFlag(4,0xbc,OracleSaveData.RoomFlagItem,false);
            LoadValidationRoom(4,0xbc);
            var outgoing=_entities.Entities<RetractableTriggerChestRoomEntity>().Single();
            var outgoingPuff=_entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(120,88),OracleSoundEngine.SndPoof));
            FailIf(outgoingPuff.Initialized || outgoingPuff.Visible,"INTERAC$05 allocation must wait for state0 before becoming visible.");
            Step();
            FailIf(!outgoingPuff.Initialized || !outgoingPuff.Visible || outgoingPuff.ElapsedUpdates!=1,
                "INTERAC$05 state0 must initialize visibility without advancing animation.");
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            var incomingChest=_entities.Entities<RetractableTriggerChestRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(outgoing)!=2 || _entities.InteractionSlot(outgoingPuff)!=3 ||
                _entities.InteractionSlot(incomingChest)!=4,
                "Incoming chest must allocate after the retained outgoing controller and puff slots $d2/$d3.");
            _sound.ClearPlayRequestAudit();
            Triggers(0x0f); Step();
            FailIf(!outgoing.Finished || _currentRoom.Layout[0x57]!=0xf1,
                "Scrolling must delete the outgoing controller and dispatch incoming state0.");
            var incomingPuff=_entities.Entities<PuzzlePuffEffect>().Single();
            FailIf(incomingPuff.ElapsedUpdates!=0 || incomingPuff.Visible || outgoingPuff.ElapsedUpdates!=2 ||
                _entities.InteractionSlot(incomingPuff)!=2 || _sound.PlayRequestsFor(OracleSoundEngine.SndPoof)!=0,
                "Incoming chest must reuse the deleted outgoing controller's lower slot $d2 and defer puff initialization until the next pass.");
            // interactionAnimation5a0f2 is $06,$08,$04, then parameter $ff.
            // State1 tests the parameter BEFORE animation; deletion follows
            // the terminal parameter on the next update, even while scrolling.
            Step(17);
            FailIf(outgoingPuff.ElapsedUpdates!=19 || outgoingPuff.CurrentParameter!=0xff || outgoingPuff.Finished ||
                incomingPuff.ElapsedUpdates!=17 || incomingPuff.AnimationFrame!=2,
                "INTERAC$05 scrolling lost its source $06/$08/$04 animation boundaries.");
            Step();
            FailIf(_entities.OutgoingEntities<PuzzlePuffEffect>().Count!=0 || incomingPuff.Finished || incomingPuff.ElapsedUpdates!=18,
                "Outgoing puff must delete one update after terminal parameter while the newer incoming puff remains alive.");
            Step();
            FailIf(incomingPuff.Finished || incomingPuff.CurrentParameter!=0xff,
                "Lower-slot puff must retain its terminal parameter until its next dispatch.");
            Step();
            FailIf(_entities.Entities<PuzzlePuffEffect>().Count!=0 || _sound.PlayRequestsFor(OracleSoundEngine.SndPoof)!=1,
                "Incoming puff must finish during scrolling without replaying initialization sound.");
            _entities.FinishScreenTransition();
            LoadValidationRoom(0,0x60);
        }
    }
}
