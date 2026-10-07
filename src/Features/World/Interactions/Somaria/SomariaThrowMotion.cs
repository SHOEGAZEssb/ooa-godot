using Godot;
using System;

namespace oracleofages;

// ITEM18 uses the bomb lateral helper and common vertical/hazard helper,
// but deletes on its first ground contact rather than using bomb bouncing.
internal sealed class SomariaThrowMotion(OracleRoomData room, SomariaPlacementDatabase hazards, OracleRuntimeState? memory = null)
{
    private readonly BraceletWeight _weight = new BraceletWeightDatabase().Weight(0);
    private readonly BombRecord _common = new BombDatabase().Data;
    private readonly ItemTilePassage _passage = new();
    private Vector2 _position;
    private int _z, _speedZ, _flags;
    internal Vector2 Position => _position.Floor();
    internal int ZHigh => (sbyte)(byte)(_z >> 8);
    internal int SpeedZ => _speedZ;
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    private bool Sideview => (room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0;

    internal void Begin(Vector2 position, int zHigh, int direction, int angle, bool tossRing)
    {
        if (direction is < 0 or > 3 || (angle is not (>= 0 and < 32) && angle != 0xff))
            throw new ArgumentOutOfRangeException(nameof(direction));
        Vector2 step = OracleObjectMath.StrictCardinalVector(direction*8);
        _position = new((byte)((int)position.X+(int)step.X), (byte)((int)position.Y+(int)step.Y));
        _z = zHigh << 8; Angle = angle;
        _speedZ = angle == 0xff ? 0 : _weight.InitialSpeedZ;
        Speed = angle == 0xff ? 0 : tossRing ? _weight.TossSpeedRaw : _weight.SpeedRaw;
    }

    internal void AdvanceLateral()
    {
        if ((_flags & 1) != 0) Speed = 0;
        if (Angle == 0xff) return;
        if (Angle != 0xff)
        {
            Vector2I offset = _common.EdgeOffsets[(Angle & ObjectAngle.CardinalMask) >> 3];
            Vector2 probe = new((byte)((int)Position.X+offset.X), (byte)((int)Position.Y+offset.Y));
            if (probe.Y < 0xb0 && room.IsSolid(probe) && !_passage.CanPass(room, probe, Angle)) Angle = 0xff;
        }
        // A newly blocked angle $ff falls through and clears velocity;
        // an angle already $ff returned above before the collision probe.
        if (!Sideview || (Angle & 15) != 0)
            NativeObjectMovement.ApplySpeed(memory, ref _position, Speed, Angle);
    }

    // Returns true on contact with solid ground. A destructive hazard sets
    // delete=true instead; sideview water emits a splash but keeps falling.
    internal bool AdvanceVertical(Action<int, Vector2, int> effect, out bool delete)
    {
        delete = false;
        if (!Sideview)
        {
            if (!OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, _weight.Gravity)) return false;
            int hazard = HazardBelow();
            if (hazard == 0) return true;
            effect(hazard, Position, ZHigh); delete = true; return false;
        }

        bool landed = ItemVerticalMotion.AdvanceSideview(room, ref _position, ref _z,
            ref _speedZ, ref _flags, _weight.Gravity, out int kind);
        if (kind != 0) effect(kind, Position, 0);
        delete = kind == 4;
        return landed;
    }

    private int HazardBelow() => hazards.Hazard(room.ActiveCollisions,
        room.GetMetatile(new(Position.X, (byte)((int)Position.Y+5))));
}
