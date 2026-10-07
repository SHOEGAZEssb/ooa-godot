using Godot;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownShutterContact()
    {
        var contact = typeof(DungeonDoorRoomEntity).GetMethod("OverlapsLink",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rom = new LinkCollisionRom();
        rom[0xffae] = 0x40; // hActiveObjectType: INTERACTION, D=$d2.
        rom[0xd026] = rom[0xd027] = 6;
        int checkedDoors = 0;
        // Seven original placements and commonScripts directional radii.
        // This predicate test supplies byte XY directly; reachable gameplay
        // approaches and script handoffs are checked by shutter timing.
        foreach (int room in new[] { 0x9d,0xa6,0xb4,0xb6,0xbf })
        {
            LoadValidationRoom(4,room);
            foreach (var door in _entities.Entities<DungeonDoorRoomEntity>())
            {
                checkedDoors++;
                bool vertical = (door.SubId&1) == 0;
                rom[0xd24b] = (byte)door.Position.Y; rom[0xd24d] = (byte)door.Position.X;
                rom[0xd266] = (byte)(vertical ? 10 : 8);
                rom[0xd267] = (byte)(vertical ? 8 : 10);
                foreach (bool horizontal in new[] { false,true })
                {
                    int radius = horizontal ? vertical ? 14 : 16 : vertical ? 16 : 14;
                    foreach (var (distance,expected) in new[] { (-radius-1,false),(-radius,true),(radius-1,true),(radius,false) })
                    foreach (byte z in new byte[] { 0,7,0x80 })
                    {
                        Vector2 point = door.Position+(horizontal ? new Vector2(distance,0) : new Vector2(0,distance));
                        _player.WarpTo(point); _player.SetScriptedZHigh(z);
                        rom[0xd00b] = (byte)point.Y; rom[0xd00d] = (byte)point.X; rom[0xd00f] = z;
                        rom.Call(0x1c6f,bank:0,objectPage:0xd2); // objectCheckCollidedWithLink_ignoreZ.
                        bool native = (rom[0xc201]&0x10) != 0;
                        FailIf(native != expected || (bool)contact.Invoke(door,[_player])! != native,
                            $"Native Crown shutter4:{room:x2}/${door.SubId:x2} contact differs at {(horizontal ? "X" : "Y")} offset{distance}, Z${z:x2}.");
                    }
                }
            }
        }
        _player.SetScriptedZHigh(0);
        FailIf(checkedDoors != 7,"Crown's seven original shutter placements must all retain native contact checks.");
        LoadValidationRoom(0,0x60);
    }
}
