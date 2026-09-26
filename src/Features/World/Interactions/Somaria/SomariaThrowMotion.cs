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

        // Merge only the high Z byte, preserving fractional Y, as the native
        // itemMergeZPositionIfSidescrollingArea does on every vertical call.
        _position.Y = (byte)((int)Position.Y+ZHigh) + (_position.Y-Mathf.Floor(_position.Y));
        _z = 0;
        int kind = HazardBelow(), old = _flags;
        _flags = ((_flags & 0xb8) ^ 0x80) | kind;
        if (((kind ^ old) & 1) != 0) _flags |= 0x40;
        bool rising = _speedZ < 0;
        Vector2 probe = rising ? Position : new(Position.X, (byte)((int)Position.Y+5));
        bool collision = room.IsSolid(probe);
        if (!rising && collision) { _flags |= 0x10; return true; }
        _flags &= ~0x10;
        // A ceiling blocks displacement but still applies gravity. Water
        // skips alternate falling updates and caps downward speed to $0100.
        bool pauseWater = !collision && (kind & 1) != 0 && (_flags & 0x80) != 0;
        if (!pauseWater)
        {
            if (!collision)
            {
                int y = (int)Mathf.Round(_position.Y*256);
                _position.Y = unchecked((ushort)(y+_speedZ))/256.0f;
            }
            _speedZ = unchecked((short)(_speedZ+_weight.Gravity));
            int maximum = collision || (kind & 1) == 0 ? 0x300 : 0x100;
            if (_speedZ >= maximum) _speedZ = maximum;
        }
        if ((kind & 4) != 0) { effect(4, Position, 0); delete = true; }
        else if ((_flags & 0x40) != 0) effect(1, Position, 0);
        return false;
    }

    private int HazardBelow() => hazards.Hazard(room.ActiveCollisions,
        room.GetMetatile(new(Position.X, (byte)((int)Position.Y+5))));
}
