using Godot;
using System;

namespace oracleofages;

// Physical ITEM_BRACELET in reserved slot C. Its fractional coordinates are
// distinct from the related native enemy's high-byte position copies.
internal sealed class NativeBraceletThrow(OracleRoomData room, int weight,
    Func<bool> relatedHeld, Func<bool> relatedThrowing, Func<Vector2> relatedPosition,
    Func<int> relatedZ, Action<Vector2, int> copyPosition, Action<int> release,
    Action finishBounce, Func<OracleObjectPosition, int, int, OracleObjectPosition> move,
    BraceletWeightDatabase weights, BombRecord commonThrow)
{
    private readonly BraceletWeight _weight = weights.Weight(weight);
    private int _z, _speedZ;
    internal bool Active { get; private set; }
    internal Vector2 Position { get; private set; }
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    internal int Speed { get; private set; }
    internal int Angle { get; private set; } = 0xff;

    internal void Hold(Vector2 linkPosition, int linkZHigh, int frame, int direction)
    {
        if (!relatedHeld())
            throw new InvalidOperationException("ITEM_BRACELET $16 held-position copy requires a grabbed related object.");
        var offset = weights.LiftOffset(weight, frame, direction);
        copyPosition(new((byte)((int)Mathf.Floor(linkPosition.X) + offset.X),
            (byte)(int)Mathf.Floor(linkPosition.Y)), unchecked((sbyte)(linkZHigh + offset.Y)));
    }

    internal void Begin(int direction, int angle, bool tossRing)
    {
        if ((room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0)
            throw new NotSupportedException("ITEM_BRACELET $16 reserved object throw requires its top-down room.");
        if (!relatedHeld())
            throw new InvalidOperationException("ITEM_BRACELET $16 release requires a held related object.");
        Vector2I step = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, 3 => Vector2I.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)) };
        // New slot's low bytes start at zero. itemBeginThrow offsets high XY
        // once in Link's facing direction even for an angle-$ff drop.
        Vector2 position = relatedPosition();
        Position = new((byte)((int)position.X + step.X), (byte)((int)position.Y + step.Y));
        _z = (relatedZ() >> 8) << 8;
        SetAngle(angle);
        _speedZ = angle == 0xff ? 0 : _weight.InitialSpeedZ;
        Speed = angle == 0xff ? 0 : tossRing ? _weight.TossSpeedRaw : _weight.SpeedRaw;
        Active = true;
        release(angle);
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
        if (!relatedThrowing()) { Active = false; return; }
        if (Angle != 0xff)
        {
            var offset = commonThrow.EdgeOffsets[(Angle & ObjectAngle.CardinalMask) >> 3];
            var probe = new Vector2((byte)((int)Position.X + offset.X), (byte)((int)Position.Y + offset.Y));
            if (probe.Y < 0xb0 && room.IsSolid(probe) && !commonThrow.CanPassSolidTile(room, probe)) Angle = 0xff;
            // A newly blocked throw falls through objectApplySpeed with $ff,
            // clearing velocity scratch. Later $ff updates return above.
            Position = move(
                OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
        }
        if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, _weight.Gravity))
        {
            sound(SoundId.SndBombLand);
            int rebound = unchecked((short)-_speedZ) >> 1;
            if ((ushort)rebound > 0xff80 || rebound == 0)
            {
                // @release precedes @noCollision's objectCopyPosition.
                finishBounce(); Active = false; return;
            }
            _speedZ = rebound;
            Speed = commonThrow.ReducedBounceSpeed(Speed);
        }
        copyPosition(Position, _z >> 8);
    }
}
