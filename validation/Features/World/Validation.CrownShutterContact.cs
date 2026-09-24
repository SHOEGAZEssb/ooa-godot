using Godot;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownShutterContact()
    {
        var contact=typeof(DungeonDoorRoomEntity).GetMethod("OverlapsLink",BindingFlags.Instance|BindingFlags.NonPublic)!;
        int checkedDoors=0;
        // Crown's source INTERAC$1e placements: trigger down/left, miniboss
        // left/up, enemy up, boss up/left. Test just the contact predicate.
        foreach(int room in new[]{0x9d,0xa6,0xb4,0xb6,0xbf})
        {
            LoadValidationRoom(4,room);
            foreach(var door in _entities.Entities<DungeonDoorRoomEntity>())
            {
                checkedDoors++;
                foreach(bool horizontal in new[]{false,true})
                {
                    // commonScripts.s radii Y/X=$0a/$08 vertically,
                    // $08/$0a horizontally, plus Link radius$06.
                    int radius=horizontal ? (door.SubId%2==0 ? 14 : 16) : (door.SubId%2==0 ? 16 : 14);
                    foreach(var (distance,expected) in new[]{(-radius-1,false),(-radius,true),(radius-1,true),(radius,false)})
                    {
                        _player.WarpTo(door.Position+(horizontal?new Vector2(distance,0):new Vector2(0,distance)));
                        _player.SetScriptedZHigh(0x80);
                        FailIf((bool)contact.Invoke(door,[_player])! != expected,
                            $"Crown shutter4:{room:x2}/${door.SubId:x2} contact differs at {(horizontal?"X":"Y")} offset{distance}; height must be ignored.");
                    }
                }
            }
        }
        _player.SetScriptedZHigh(0);
        FailIf(checkedDoors!=7,"Crown's seven source shutter placements must all have contact checks.");
        LoadValidationRoom(0,0x60);
    }
}
