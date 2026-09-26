using Godot;
using System;

namespace oracleofages;

// link.s:specialObjectUpdatePositionGivenVelocity, shared by Link, raft,
// and commonCode.s:companionUpdateMovement. Callers own collision probes.
internal static class SpecialObjectMovement
{
    internal static void ApplySpeed(ref Vector2 position, int speed, int angle, int walls)
    {
        if ((angle & 0x80) != 0) return;
        if (angle is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(angle));
        if (TryAdjustCardinalAngle(angle, walls, out int adjustedAngle))
        {
            angle = adjustedAngle;
            walls = 0; // Successful @tileEdgeAdjust clears e before masking.
        }
        int blocked = walls & BitsToCheck(angle);
        Vector2 next = position;
        OracleObjectMovement.Shared.ApplySpeed(ref next, speed, angle);
        if ((blocked & 0xf0) == 0) position.Y = next.Y;
        if ((blocked & 0x0f) == 0) position.X = next.X;
    }

    internal static int BitsToCheck(int angle)
    {
        return angle switch
        {
            ObjectAngle.Up => 0xcf,
            >= 1 and <= 7 => 0xc3,
            ObjectAngle.Right => 0xf3,
            >= 9 and <= 15 => 0x33,
            ObjectAngle.Down => 0x3f,
            >= 17 and <= 23 => 0x3c,
            ObjectAngle.Left => 0xfc,
            >= 25 and <= 31 => 0xcc,
            _ => 0xff
        };
    }

    internal static bool TryAdjustCardinalAngle(int angle, int walls, out int adjustedAngle)
    {
        adjustedAngle = angle;
        // slideAngleTable permits each cardinal and its two immediate
        // neighbors (31/0/1, 7/8/9, 15/16/17, 23/24/25).
        int sector = (angle + 1) & ObjectAngle.Mask;
        if ((sector & 7) > 2) return false;
        switch (sector & 0x18)
        {
            case 0:
                if ((walls & 0xc3) == 0x80) adjustedAngle = 8;
                else if ((walls & 0xcc) == 0x40) adjustedAngle = 24;
                else return false;
                return true;
            case 8:
                if ((walls & 0xc3) == 0x01) adjustedAngle = 0;
                else if ((walls & 0x33) == 0x02) adjustedAngle = 16;
                else return false;
                return true;
            case 16:
                if ((walls & 0x33) == 0x20) adjustedAngle = 8;
                else if ((walls & 0x3c) == 0x10) adjustedAngle = 24;
                else return false;
                return true;
            case 24:
                if ((walls & 0xcc) == 0x04) adjustedAngle = 0;
                else if ((walls & 0x3c) == 0x08) adjustedAngle = 16;
                else return false;
                return true;
            default:
                return false;
        }
    }

}
