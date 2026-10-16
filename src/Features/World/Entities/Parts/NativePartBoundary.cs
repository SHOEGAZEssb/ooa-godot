using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal static class NativePartBoundary
{
    // partCommon_checkOutOfBounds rounds non-cardinal angles toward the
    // quadrant diagonal, probes its source offset, and tests only $ff.
    internal static bool IsInside(OracleRoomData room,Vector2 position,int angle,IReadOnlyList<byte> offsets)
    {
        int rounded=(angle&7)==0?angle:(angle&0x18)+4;
        int at=rounded/2;
        int x=((int)position.X+unchecked((sbyte)offsets[at+1]))&255;
        int y=((int)position.Y+unchecked((sbyte)offsets[at]))&255;
        return room.GetTerrainInfo(new(x,y)).Collision!=255;
    }
}
