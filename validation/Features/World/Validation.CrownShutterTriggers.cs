using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterTriggers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var data = new DungeonMechanicDatabase();
        LoadValidationRoom(4, 0x9d);
        var source = data.GetRoomRecords(4, 0x9d).Single(r => r.Id == 0x1e);
        var sounds = new List<int>();
        var spawns = new List<RoomEntitySpawn>();
        bool active = false;
        // scriptHelper.s uses exactly $78/$79/$7a/$7b for orientations0..3.
        // Tile/layout and collision writes are independent, including Somaria
        // ($da) and the retained collision byte during shutter interleaving.
        for (int direction = 0; direction < 4; direction++)
        {
            var door = new DungeonDoorRoomEntity(source with { SubId = 4 + direction },
                _currentRoom, data, () => 1, _ => active, p => p, () => 0,
                sounds.Add, default, true);
            try
            {
                foreach (bool triggered in new[] { false, true })
                foreach (byte tile in new byte[] { 0x78, 0x79, 0x7a, 0x7b, 0xa0, 0xda })
                foreach (byte collision in new byte[] { 0, 1, 2, 4, 8, 0x0f, 0x10, 0xff })
                {
                    active = triggered;
                    state.SetValue(door, DoorState.WatchingTrigger);
                    _currentRoom.SetPositionTileAndCollision(door.Position, tile, collision, 0);
                    sounds.Clear();
                    door.UpdateFrame(new RoomEntityFrame(_player, 0, false), spawns);
                    DoorState expected = triggered
                        ? tile == 0x78 + direction ? DoorState.PlayTriggerSolve : DoorState.WatchingTrigger
                        : collision == 0 ? DoorState.SelectClosing : DoorState.WatchingTrigger;
                    FailIf((DoorState)state.GetValue(door)! != expected ||
                        sounds.Count != 0 ||
                        _currentRoom.GetMetatile(door.Position) != tile ||
                        _currentRoom.GetTerrainInfo(door.Position).Collision != collision,
                        $"Shutter${4 + direction:x2} trigger={triggered}, tile${tile:x2}, collision${collision:x2} must select {expected} without changing terrain.");
                }
            }
            finally { door.Free(); }
        }
        LoadValidationRoom(0, 0x60);
    }
}
