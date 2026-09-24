using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterSomaria()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var counter = typeof(DungeonDoorRoomEntity).GetField("_counter", flags)!;
        var placementData = new SomariaPlacementDatabase();
        foreach (bool batch in new[] { false, true })
        foreach (bool somaria in new[] { false, true })
        {
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
            LoadValidationRoom(4, 0x9d);
            var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
            _player.WarpTo(new(120, 136));
            FailIf(_collision.Collides(_player.Position), "Shutter fixture must leave Link on real floor away from the door.");
            Step();
            // Isolate the handoff after the script chose state3, before its
            // next INTERACTION pass. ITEM18 may place a block in that gap.
            _currentRoom.SetPositionTileAndCollision(door.Position, 0xa0, 0, 0);
            var placement = new SomariaBlockPlacement(_currentRoom, placementData);
            if (somaria)
                FailIf(!placement.TryCreate(4, door.Position, 0, 0) || !placement.InPlace,
                    "Somaria placement must write the open shutter tile before closing begins.");
            else
                _currentRoom.SetPositionTileAndCollision(door.Position, 0xa1, 0x0f, 0);
            state.SetValue(door, DoorState.ReadyToClose);
            Step();
            if (!somaria)
            {
                FailIf(_currentRoom.GetMetatile(door.Position) != 0xa1,
                    "A closing shutter must not overwrite an unrelated solid tile.");
                continue;
            }
            FailIf((DoorState)state.GetValue(door)! != DoorState.ClosingInterleaved ||
                (int)counter.GetValue(door)! != 6 || _currentRoom.GetMetatile(door.Position) != 0xa0 ||
                _currentRoom.GetTerrainInfo(door.Position).Collision != 0x0f || placement.InPlace,
                "Closing must replace Somaria's layout byte immediately, retaining collision for six updates.");
            FailIf(placement.Remove(0), "A displaced Somaria block must not restore its underlying floor.");
            Step(5);
            FailIf((int)counter.GetValue(door)! != 1 || _currentRoom.GetMetatile(door.Position) != 0xa0,
                "The shutter's final tile must wait for the sixth animation update.");
            Step();
            FailIf(_currentRoom.GetMetatile(door.Position) != 0x7a ||
                _currentRoom.GetTerrainInfo(door.Position).Collision != 0x0f || placement.Remove(0),
                "The down-facing shutter must remain$7a after stale Somaria cleanup.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
