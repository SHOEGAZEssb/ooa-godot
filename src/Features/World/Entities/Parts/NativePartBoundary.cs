using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal static class NativePartBoundary
{
    // The common helper's Z flag is only set by raw $ff + 1. Ordinary
    // wall collisions return carry with Z clear; RET NZ callers ignore them.
    internal static bool IsScreenBoundaryAtPoint(OracleRoomData room,Vector2 position) =>
        room.GetTerrainInfo(new(OracleObjectPosition.HighByte(position.X),OracleObjectPosition.HighByte(position.Y))).Collision==255;
    // partCommon_checkTileCollisionOrOutOfBounds tests the point itself:
    // raw $ff first, then checkTileCollision_allowHoles at wrapped high YX.
    internal static bool HasTileCollisionOrBoundary(OracleRoomData room, Vector2 position)
    {
        Vector2 point = new(OracleObjectPosition.HighByte(position.X), OracleObjectPosition.HighByte(position.Y));
        return room.GetTerrainInfo(point).Collision == 0xff || room.IsSolid(point);
    }

    // partCommon_checkOutOfBounds rounds non-cardinal angles toward the
    // quadrant diagonal, probes its source offset, and tests only $ff.
    internal static bool IsInside(OracleRoomData room,Vector2 position,int angle,IReadOnlyList<byte> offsets)
    {
        int at=RoundAngle(angle)/2;
        return Probe(room,position,unchecked((sbyte)offsets[at]),unchecked((sbyte)offsets[at+1]));
    }

    internal static bool IsInside(OracleRoomData room,Vector2 position,int angle,IReadOnlyList<EnemyBehaviorPair> offsets)
    {
        var offset=offsets[RoundAngle(angle)/4];
        return Probe(room,position,offset.First,offset.Second);
    }

    private static int RoundAngle(int angle) => (angle&7)==0?angle:(angle&0x18)+4;

    private static bool Probe(OracleRoomData room,Vector2 position,int yOffset,int xOffset)
    {
        int x=(OracleObjectPosition.HighByte(position.X)+xOffset)&255;
        int y=(OracleObjectPosition.HighByte(position.Y)+yOffset)&255;
        return room.GetTerrainInfo(new(x,y)).Collision!=255;
    }
}
