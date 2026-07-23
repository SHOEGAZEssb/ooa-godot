using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common PART_BUTTON $09 handler. Subid bit 7 selects a reusable pressure
/// button; bits 0-2 select the shared wActiveTriggers bit.
/// </summary>
internal sealed partial class GroundButtonRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Action<int, bool> _setTrigger;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private bool _initialized;
    private bool _pressed;
    private bool _latchedBelowObject;
    private int _releaseCounter;

    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal int TriggerBit => _record.SubId & 0x07;
    internal bool Reusable => (_record.SubId & 0x80) != 0;
    internal bool Pressed => _pressed;
    internal int ReleaseCounter => _releaseCounter;
    public bool Finished { get; private set; }

    internal GroundButtonRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Action<int, bool> setTrigger,
        Func<long> animationTick,
        Action<int> playSound)
        : base(record, $"GroundButton_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != 0x09)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _setTrigger = setTrigger;
        _animationTick = animationTick;
        _playSound = playSound;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // State 0 copies subid bits 0-2 to var03 and returns without checking
        // pressure until the following update.
        if (!_initialized)
        {
            _initialized = true;
            return;
        }

        byte tile = _room.GetMetatile(Position);
        if (_latchedBelowObject)
        {
            if (tile != _data.ButtonTile && tile != _data.PressedButtonTile)
                return;
            SetButtonTile((byte)_data.PressedButtonTile);
            Finished = true;
            return;
        }

        if (TouchesLink(frame.Player))
        {
            if (_pressed)
                return;
            Press();
            if (tile == _data.ButtonTile || tile == _data.PressedButtonTile)
            {
                SetButtonTile((byte)_data.PressedButtonTile);
                if (!Reusable)
                    Finished = true;
            }
            else if (!Reusable)
            {
                // setTileInRoomLayoutBuffer leaves the object above the
                // button visible. Keep a tiny helper alive until the runtime
                // push-block controller restores the underlying tile.
                _latchedBelowObject = true;
            }
            return;
        }

        bool somethingOnButton = tile != _data.ButtonTile &&
            tile != _data.PressedButtonTile;
        if (somethingOnButton)
        {
            if (_pressed)
                return;
            Press();
            if (Reusable)
            {
                _releaseCounter = _data.ButtonObjectReleaseDelay;
            }
            else
            {
                _latchedBelowObject = true;
            }
            return;
        }

        if (_releaseCounter != 0)
        {
            // A stationary object hides the button tile. The original writes
            // $0d to wRoomLayoutBuffer, so it is revealed pressed when the
            // object moves; the runtime represents that reveal explicitly.
            if (tile == _data.ButtonTile)
                SetButtonTile((byte)_data.PressedButtonTile);
            _releaseCounter--;
            if (_releaseCounter != 0)
                return;
        }

        if (!_pressed)
            return;
        SetButtonTile((byte)_data.ButtonTile);
        _setTrigger(TriggerBit, false);
        _pressed = false;
        _playSound(_data.ButtonSound);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private bool TouchesLink(Player player)
    {
        if (!player.IsGroundedForFloorButton)
            return false;
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 button = OracleObjectMath.ToPixelPosition(Position);
        return Mathf.Abs(link.Y - button.Y) <
                _data.ButtonRadiusY + NpcCharacter.LinkCollisionRadius &&
            Mathf.Abs(link.X - button.X) <
                _data.ButtonRadiusX + NpcCharacter.LinkCollisionRadius;
    }

    private void Press()
    {
        _setTrigger(TriggerBit, true);
        _pressed = true;
        _playSound(_data.ButtonSound);
    }

    private void SetButtonTile(byte tile) => _room.SetPositionTileAndCollision(
        Position, tile, null, _animationTick());
}
