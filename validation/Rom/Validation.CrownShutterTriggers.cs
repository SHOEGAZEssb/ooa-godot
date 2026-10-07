using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterTriggers()
    {
        var state = typeof(DungeonDoorRoomEntity).GetField("_state",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var data = new DungeonMechanicDatabase();
        LoadValidationRoom(4,0x9d);
        var source = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e);
        var sounds = new List<int>();
        var spawns = new List<RoomEntitySpawn>();
        bool active = false;
        // Isolate the decision predicate. Actual PART/INTERACTION publication,
        // script yields, completion and repeat are exercised by shutter timing.
        // Original bank$15:$40a0 writes its decision to wTmpcfc0+$01.
        var rom = new FrontendRom();
        byte[] caller = [0x16,0xd2,0xcd,0xa0,0x40,0xc9];
        for (int index = 0; index < caller.Length; index++) rom[0xc100+index] = caller[index];
        rom[0xd27d] = 1; // Original var3d trigger mask for source parameter0.
        rom[0xd27e] = (byte)source.PackedPosition;
        for (int direction = 0; direction < 4; direction++)
        {
            var door = new DungeonDoorRoomEntity(source with { SubId = 4+direction },
                _currentRoom,data,() => 1,_ => active,p => p,() => 0,
                sounds.Add,default,true,_rooms.TrySetTile);
            try
            {
                rom[0xd249] = (byte)(0x10+direction*2);
                foreach (bool triggered in new[] { false,true })
                foreach (byte tile in new byte[] { 0x78,0x79,0x7a,0x7b,0xa0,0xda })
                foreach (byte collision in new byte[] { 0,1,2,4,8,0x0f,0x10,0xff })
                {
                    active = triggered;
                    state.SetValue(door,DoorState.WatchingTrigger);
                    _currentRoom.SetPositionTileAndCollision(door.Position,tile,collision,0);
                    rom[0xcca0] = (byte)(triggered ? 0x81 : 0x80);
                    rom[0xcf00+source.PackedPosition] = tile;
                    rom[0xce00+source.PackedPosition] = collision;
                    rom[0xcfc1] = 0xff;
                    sounds.Clear();
                    rom.Call(0xc100,0x15);
                    door.UpdateFrame(new RoomEntityFrame(_player,0,false),spawns);
                    // Independently traced directional bytes78..7b, complete
                    // collision-byte zero test, and decision codes0/1/2.
                    int decision = triggered ? tile == 0x78+direction ? 1 : 0 : collision == 0 ? 2 : 0;
                    DoorState expected = decision == 1 ? DoorState.PlayTriggerSolve :
                        decision == 2 ? DoorState.SelectClosing : DoorState.WatchingTrigger;
                    FailIf(rom[0xcfc1] != decision || (DoorState)state.GetValue(door)! != expected ||
                        sounds.Count != 0 || rom.Sounds.Count != 0 || spawns.Count != 0 ||
                        _currentRoom.GetMetatile(door.Position) != tile ||
                        _currentRoom.GetTerrainInfo(door.Position).Collision != collision ||
                        rom[0xcf00+source.PackedPosition] != tile || rom[0xce00+source.PackedPosition] != collision ||
                        rom[0xcca0] != (triggered ? 0x81 : 0x80),
                        $"Native shutter${4+direction:x2} trigger={triggered}, tile${tile:x2}, collision${collision:x2} must select {expected} without terrain/cue/trigger side effects.");
                }
            }
            finally { door.Free(); }
        }
        LoadValidationRoom(0,0x60);
    }
}
