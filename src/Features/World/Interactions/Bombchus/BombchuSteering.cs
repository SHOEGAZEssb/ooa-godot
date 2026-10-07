using System;

namespace oracleofages;

// Native ITEM$0d angle/direction and var31-var34. The physical item will own
// position/velocity and provide front collisions in the original read order.
internal sealed class BombchuSteering
{
    internal int Angle { get; private set; }
    internal int Direction { get; private set; } = 0xff;
    internal int Turn { get; private set; }
    internal bool Clinging { get; private set; }
    internal int FormerAngle { get; private set; }
    internal bool Ceiling { get; private set; }
    internal int Animation { get; private set; }

    internal BombchuSteering(BombchuDatabase data, int direction, bool sidescroll)
    {
        Angle = (direction & 3) * 8;
        Turn = data.Constant("initial-turn");
        SetAnimation();
        if (sidescroll && (Angle & 8) == 0)
        {
            Angle += 8;
            SetAnimation();
        }
    }

    internal bool Home(int relativeAngle, bool sidescroll)
    {
        relativeAngle &= 31;
        if (sidescroll)
            Angle = (Angle & 8) != 0 ? (relativeAngle < 16 ? 8 : 24) :
                (((relativeAngle - 8) & 31) < 16 ? 0 : 16);
        else
        {
            Angle = (relativeAngle + 4) & 0x18;
            Turn = ((Angle - relativeAngle) & 31) < 16 ? 0xf8 : 8;
        }
        return SetAnimation();
    }

    internal bool TurnFromImpassableTile()
    {
        Angle = (Angle + Turn) & 0x18;
        return SetAnimation();
    }

    internal BombchuClimbResult CheckWalls(Func<int, int> frontCollision)
    {
        bool resetSpeedZ = false;
        if (!Clinging)
        {
            int collision = frontCollision(Angle);
            if (collision == 0) return default;
            if (collision == 0xff) Angle ^= 0x10;
            else
            {
                FormerAngle = Angle;
                Angle = 0;
                Clinging = true;
            }
        }
        else if (frontCollision(FormerAngle) == 0)
        {
            if (FormerAngle == 0) FormerAngle = Angle;
            Angle = FormerAngle;
            Clinging = Ceiling = false;
            Direction = 0xff;
            resetSpeedZ = true;
        }
        else
        {
            if (frontCollision(Angle) == 0) return default;
            int oldAngle = Angle;
            Angle = FormerAngle ^ 0x10;
            if ((Angle & 8) != 0 && (oldAngle & 8) != 0) Clinging = false;
            FormerAngle = oldAngle;
            Ceiling = oldAngle == 0;
        }
        return new(SetAnimation(), resetSpeedZ);
    }

    private bool SetAnimation()
    {
        if (Direction == Angle) return false;
        Direction = Angle;
        Animation = Ceiling ? (Angle == 8 ? 4 : 5) : Angle / 8;
        return true;
    }
}

internal readonly record struct BombchuClimbResult(bool AnimationChanged, bool ResetSpeedZ);
