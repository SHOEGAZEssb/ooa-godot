using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogDoorAlias()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var counter = typeof(DungeonDoorRoomEntity).GetField("_counter", flags)!;
        var data = new DungeonMechanicDatabase();
        foreach (bool batch in new[] { false, true })
        foreach (int subid in new[] { 8, 11 })
        foreach (bool opening in new[] { false, true })
        {
            LoadValidationRoom(4, 0xbf); _entities.Clear(); _player.WarpTo(new(120,120));
            bool text = false;
            int enemies = 1;
            var door = new DungeonDoorRoomEntity(data.GetRoomRecords(4,0xbf).Single(r => r.SubId == subid),
                _currentRoom, data, () => enemies, _ => false, p => p, () => 0, _ => { }, default, true,
                textActive: () => text);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [door]);
            void Step(int n = 1) => StepGameplayUpdates(n, Vector2.Zero, batched: batch);
            void Write(int value) => typeof(RoomEntityManager).GetMethod("WriteSmogInteractionCounter",flags)!
                .Invoke(_entities, [_entities.InteractionSlot(door.Node), value]);
            Write(60); Step();
            FailIf(door.Counter2Alias != 0 || (DoorState)state.GetValue(door)! != DoorState.SetAngle,
                "Door state0 must clear inherited counter2 through interactionSetScript.");
            Write(3); text = true; Step(2);
            FailIf(door.Counter2Alias != 3, "Paused door scripts must not decrement counter2.");
            text = false; Step(3);
            FailIf(door.Counter2Alias != 0 || (DoorState)state.GetValue(door)! != DoorState.SetAngle,
                "Door scripts must still return on counter2's 1->0 update.");
            Step();
            FailIf((DoorState)state.GetValue(door)! != DoorState.InitialBranch,
                "The saved door command must resume on the update after counter2 reaches zero.");

            state.SetValue(door, DoorState.SolveDelay); counter.SetValue(door,2); Write(3);
            Step();
            FailIf((int)counter.GetValue(door)! != 1 || door.Counter2Alias != 3,
                "Door counter1 must take precedence over counter2.");
            Step();
            FailIf((int)counter.GetValue(door)! != 0 || door.Counter2Alias != 2,
                "Counter2 must decrement on the same update counter1 reaches zero.");
            Step(2);
            FailIf((DoorState)state.GetValue(door)! != DoorState.SolveDelay || door.Counter2Alias != 0,
                "Counter2's terminal update must not execute the door setstate command.");
            Step();
            FailIf((DoorState)state.GetValue(door)! != DoorState.ReadyToOpen,
                "The door's setstate command must run after both waits finish.");
            if (!opening)
            {
                state.SetValue(door, DoorState.ReadyToClose);
                _currentRoom.SetPositionTileAndCollision(door.Position, (byte)data.OpenTile, null, 0);
            }
            Write(60); Step(); Step(5);
            FailIf(door.Counter2Alias != 60 || (DoorState)state.GetValue(door)! !=
                (opening ? DoorState.OpeningInterleaved : DoorState.ClosingInterleaved),
                "Native door animation must bypass script counter2.");
            Step();
            FailIf(door.Counter2Alias != 59 || _currentRoom.IsSolid(door.Position) == opening || door.Finished,
                "Animation completion must resume the script and decrement counter2 in the same update.");
            Step(59);
            FailIf(door.Counter2Alias != 0 || door.Finished, "Door scriptend must await the update after counter2 expires.");
            enemies = 0; Step();
            FailIf(opening ? !door.Finished : (DoorState)state.GetValue(door)! != DoorState.PlayEnemySolve,
                "The resumed enemy-door command must delete after opening or observe enemy count after closing.");
        }
        LoadValidationRoom(0,0x60);
    }
}
