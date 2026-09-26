using Godot;
using System;

namespace oracleofages;

// Physical ITEM_BRACELET in reserved slot C. Its fractional coordinates are
// distinct from the related native enemy's high-byte position copies.
internal sealed class SmasherBraceletThrow(SmasherCharacter ball, OracleRoomData room,
    BraceletWeightDatabase weights, BombRecord commonThrow)
{
    private readonly BraceletWeight _weight = weights.Weight(2);
    private int _z, _speedZ;
    internal bool Active { get; private set; }
    internal Vector2 Position { get; private set; }
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    internal int Speed { get; private set; }
    internal int Angle { get; private set; } = 0xff;

    internal void Hold(Vector2 linkPosition, int linkZHigh, int frame, int direction)
    {
        if (!ball.IsBall || ball.State != 2 || ball.GrabSubstate >= 2)
            throw new InvalidOperationException("$74 held-position copy requires the grabbed ball.");
        var offset = weights.LiftOffset(2, frame, direction);
        ball.CopyCarriedPosition(new((byte)((int)Mathf.Floor(linkPosition.X) + offset.X),
            (byte)(int)Mathf.Floor(linkPosition.Y)), unchecked((sbyte)(linkZHigh + offset.Y)));
    }

    internal void Begin(int direction, int angle, bool tossRing)
    {
        if ((room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0)
            throw new NotSupportedException("Smasher $74 reserved bracelet throw requires its top-down room.");
        if (!ball.IsBall || ball.State != 2 || ball.GrabSubstate >= 2)
            throw new InvalidOperationException("$74 release requires a held ball before creating reserved item C.");
        Vector2I step = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, 3 => Vector2I.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)) };
        // New slot's low bytes start at zero. itemBeginThrow offsets high XY
        // once in Link's facing direction even for an angle-$ff drop.
        Position = new((byte)((int)ball.Position.X + step.X), (byte)((int)ball.Position.Y + step.Y));
        _z = (ball.ZFixed >> 8) << 8;
        SetAngle(angle);
        _speedZ = angle == 0xff ? 0 : _weight.InitialSpeedZ;
        Speed = angle == 0xff ? 0 : tossRing ? _weight.TossSpeedRaw : _weight.SpeedRaw;
        Active = true;
        ball.ReleaseGrab(angle);
    }

    internal void SetAngle(int angle)
    {
        if (angle is not (>= 0 and < 32) && angle != 0xff) throw new ArgumentOutOfRangeException(nameof(angle));
        Angle = angle;
    }

    internal void Update(Action<int> sound)
    {
        if (!Active) return;
        // braceletCheckDeleteSelfWhileThrowing runs before any movement.
        if (ball.IsDead || ball.State != 2 || ball.GrabSubstate >= 3) { Active = false; return; }
        if (Angle != 0xff)
        {
            var offset = commonThrow.EdgeOffsets[(Angle & ObjectAngle.CardinalMask) >> 3];
            var probe = new Vector2((byte)((int)Position.X + offset.X), (byte)((int)Position.Y + offset.Y));
            if (probe.Y < 0xb0 && room.IsSolid(probe) && !commonThrow.CanPassSolidTile(room, probe)) Angle = 0xff;
            // A newly blocked throw falls through objectApplySpeed with $ff,
            // clearing velocity scratch. Later $ff updates return above.
            Position = ball.ApplyMovementSpeed(
                OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
        }
        if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, _weight.Gravity))
        {
            sound(SoundId.SndBombLand);
            int rebound = unchecked((short)-_speedZ) >> 1;
            if ((ushort)rebound > 0xff80 || rebound == 0)
            {
                // @release precedes @noCollision's objectCopyPosition.
                ball.FinishGrabBounce(); Active = false; return;
            }
            _speedZ = rebound;
            Speed = commonThrow.ReducedBounceSpeed(Speed);
        }
        ball.CopyCarriedPosition(Position, _z >> 8);
    }
}
