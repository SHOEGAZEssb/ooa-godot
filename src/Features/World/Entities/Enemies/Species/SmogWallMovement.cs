using Godot;
using System;

namespace oracleofages;

// smog_state8_subid2/subid3 movement dispatch. The owning enemy must gate this
// on counter2 and run animation/projectile work first. Collision input is the
// packed 256-byte wRoomCollisions image, including byte-wrapped probes.
internal sealed class SmogWallMovement
{
    private readonly SmogWallDatabase _data;
    private readonly Func<int, byte> _collision;
    private readonly Func<int, int, OracleObjectVelocity> _velocity;
    private readonly Vector2I _originalPosition;
    private readonly int _originalDirection;
    internal int SubId { get; }
    internal OracleObjectPosition Position { get; private set; }
    internal int Direction { get; private set; }
    internal int Angle { get; private set; }
    internal int Substate { get; private set; }
    internal int AdjacentWalls { get; private set; }
    internal int WallCoordinateSum { get; private set; }
    internal int MissingWallCounter { get; private set; }

    internal SmogWallMovement(SmogWallDatabase data, int subid, OracleObjectPosition position,
        int direction, Func<int, byte> collision, Func<int, int, OracleObjectVelocity> velocity)
    {
        if ((subid & 0x7f) is not (2 or 3) || subid is < 0 or > 255 || direction is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(subid), "Smog wall movement requires subid$02/$03/$82/$83 and cardinal direction.");
        _data = data; SubId = subid; Position = position; Direction = direction;
        _velocity = velocity;
        _originalPosition = (Vector2I)position.PixelPosition; _originalDirection = direction; _collision = collision;
    }

    internal void Update(Action<Vector2> puff)
    {
        switch (Substate)
        {
            case 0:
                ApplySpeed();
                if (!CheckHuggingWall())
                {
                    MissingWallCounter = 16;
                    GoToState1();
                }
                else if (CheckHitWall()) HitWall();
                return;
            case 1:
                ApplySpeed();
                if (!CheckHuggingWall())
                {
                    if ((SubId & 0x0f) == 2)
                    {
                        MissingWallCounter = (MissingWallCounter - 1) & 255;
                        if (MissingWallCounter != 0) { GoToState1(); return; }
                        puff(Position.PixelPosition);
                        Direction = _originalDirection;
                        // Source copies only yh/xh, preserving fractions.
                        Position = new((ushort)((_originalPosition.Y << 8) | (Position.YFixed & 255)),
                            (ushort)((_originalPosition.X << 8) | (Position.XFixed & 255)));
                        puff(Position.PixelPosition);
                    }
                    else
                    {
                        if (CheckHitWall()) HitWall();
                        return;
                    }
                }
                Substate = 0;
                ApplySpeed();
                return;
            case 2:
                UpdateAdjacentWalls();
                if (CheckHitWall()) { HitWall(); return; }
                Substate = 0;
                ApplySpeed();
                return;
            default: throw new InvalidOperationException($"smog.s wall substate${Substate:x2} is not represented.");
        }
    }

    private void GoToState1()
    {
        Direction = Angle >> 3;
        Substate = 1;
        ApplySpeed();
    }
    private void HitWall()
    {
        Direction = (Direction - ((SubId & 0x80) == 0 ? 1 : -1)) & 3;
        Substate = 2;
    }
    private bool CheckHitWall()
    {
        Angle = Direction * 8;
        return CheckAdjacentWalls();
    }
    private bool CheckHuggingWall()
    {
        int side = (Direction - ((SubId & 0x80) == 0 ? -1 : 1)) & 3;
        Angle = side * 8;
        Vector2I offset = _data.FrontOffsets[side];
        int x = ((int)Position.PixelPosition.X + offset.X) & 255;
        int y = ((int)Position.PixelPosition.Y + offset.Y) & 255;
        WallCoordinateSum = (x >> 4) + (y >> 4);
        return CheckAdjacentWalls();
    }
    private bool CheckAdjacentWalls() =>
        (AdjacentWalls & (Angle switch { ObjectAngle.Up => 0xc0, ObjectAngle.Right => 0x03, ObjectAngle.Down => 0x30, ObjectAngle.Left => 0x0c,
            _ => throw new InvalidOperationException("Smog wall angle must be cardinal.") })) != 0;
    private void ApplySpeed()
    {
        Angle = Direction * 8;
        var velocity = _velocity(_data.Speeds[SubId & 0x0f], Angle);
        Position = Position.Add(velocity.YFixed, velocity.XFixed);
        UpdateAdjacentWalls();
    }
    private void UpdateAdjacentWalls()
    {
        int x = (int)Position.PixelPosition.X, y = (int)Position.PixelPosition.Y, bits = 0;
        foreach (Vector2I offset in _data.ProbeOffsets)
        {
            x = (x + offset.X) & 255; y = (y + offset.Y) & 255;
            bits = (bits << 1) | (_collision((y & 0xf0) | (x >> 4)) != 0 ? 1 : 0);
        }
        AdjacentWalls = bits;
    }
}
