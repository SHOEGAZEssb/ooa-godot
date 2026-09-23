using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal static class SmogCloudMerge
{
    internal static bool CloseEnough(int firstWall, int secondWall, Vector2 first, Vector2 second) =>
        ((firstWall - secondWall + 3) & 255) < 7 &&
        ((OracleObjectPosition.HighByte(first.Y) - OracleObjectPosition.HighByte(second.Y) + 4) & 255) < 9 &&
        ((OracleObjectPosition.HighByte(first.X) - OracleObjectPosition.HighByte(second.X) + 4) & 255) < 9;

    internal static bool TryMerge(IEnumerable<SmogCharacter> nativeSlotOrder, int phase, Action<SmogEnemySpawn> allocate)
    {
        if (phase is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(phase));
        // findNextSmogEnemy checks enabled/id/subid bit1, not size or state.
        var clouds = nativeSlotOrder.Where(cloud => !cloud.IsDead && (cloud.SubId & 2) != 0).Take(3).ToArray();
        for (int first = 0; first < clouds.Length - 1; first++)
        for (int second = first + 1; second < clouds.Length; second++)
        {
            var a = clouds[first]; var b = clouds[second];
            if (!CloseEnough(a.WallCoordinateSum,b.WallCoordinateSum,a.Position,b.Position)) continue;
            int highBit = (a.SubId ^ b.SubId) & 0x80;
            int direction = a.WallDirection;
            var position = new Vector2(OracleObjectPosition.HighByte(a.Position.X),OracleObjectPosition.HighByte(a.Position.Y));
            // Allocation occurs with both old slots still enabled/counted.
            // Preserve the source's temporary first-subid write and second
            // deletion marker before allocating the replacement.
            a.SetMergeSubId(highBit);
            b.SetMergeSubId(6);
            allocate(new(position,highBit | 3,phase,direction));
            a.SetMergeSubId(6);
            return true;
        }
        return false;
    }
}
