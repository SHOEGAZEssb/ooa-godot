using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterScroll()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
        foreach (bool batch in new[] { false, true })
        foreach (bool initialized in new[] { false, true })
        {
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
            LoadValidationRoom(4, 0x9d);
            _player.WarpTo(new(120, 136));
            var outgoing = _entities.Entities<DungeonDoorRoomEntity>().Single();
            int oldSlot = _entities.InteractionSlot(outgoing);
            if (initialized) Step();
            DoorState oldState = (DoorState)state.GetValue(outgoing)!;
            var target = _world.LoadRoom(4, 0xa6);
            _entities.BeginScreenTransition(4, target, new(240, 0), _player);
            var incoming = _entities.Entities<DungeonDoorRoomEntity>().Single();
            Vector2 targetPosition = incoming.Position;
            byte tile = target.GetMetatile(targetPosition);
            FailIf((DoorState)state.GetValue(incoming)! != DoorState.Initialize || !slots.ContainsKey(outgoing),
                "Scroll parsing must preserve the outgoing shutter allocation and leave incoming state0 pending.");
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(outgoing.Finished == initialized || slots.ContainsKey(outgoing) != initialized ||
                _entities.OutgoingEntities<DungeonDoorRoomEntity>().Count != (initialized ? 1 : 0),
                "Scroll mode$08 must dispatch/delete outgoing state0 but retain initialized shutter slots.");
            var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24, 24), 0));
            FailIf((_entities.InteractionSlot(puff) == oldSlot) == initialized,
                "Only a state0 shutter's deleted slot may be reused during the scroll.");
            Step(8);
            FailIf((DoorState)state.GetValue(incoming)! != DoorState.Initialize ||
                target.GetMetatile(targetPosition) != tile ||
                initialized && (DoorState)state.GetValue(outgoing)! != oldState ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
                "Incoming shutters and initialized outgoing shutters must keep scripts, tiles and sounds frozen through scrolling.");
            _entities.FinishScreenTransition();
            FailIf(slots.ContainsKey(outgoing) || _entities.OutgoingEntities<DungeonDoorRoomEntity>().Count != 0,
                "Scroll completion must bulk-clear the remaining outgoing shutter allocation.");
            Step();
            FailIf((DoorState)state.GetValue(incoming)! != DoorState.SetAngle,
                "Incoming shutter must execute initialization and its first script command only after scrolling ends.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
