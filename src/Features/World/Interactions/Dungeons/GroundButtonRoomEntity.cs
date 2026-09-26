using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common PART_BUTTON $09 handler. Subid bit 7 selects a reusable pressure
/// button; bits 0-2 select the shared wActiveTriggers bit.
/// </summary>
internal sealed partial class GroundButtonRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Action<int, bool> _setTrigger;
    private readonly Action<byte,byte> _setTile;
    private readonly Action<int> _playSound;
    private bool _initialized;
    private bool _pressed;
    private int _releaseCounter;

    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal int TriggerBit => _record.SubId & 0x07;
    internal bool Reusable => (_record.SubId & 0x80) != 0;
    internal bool Pressed => _pressed;
    internal int ReleaseCounter => _releaseCounter;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal GroundButtonRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Action<int, bool> setTrigger,
        Action<byte,byte> setTile,
        Action<int> playSound)
        : base(record, $"GroundButton_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != InteractionId.SnowDebris)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _setTrigger = setTrigger;
        _setTile = setTile;
        _playSound = playSound;
        Visible = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("PART$09 state0 pressure check requires the live Link during preload.");

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null)
            throw new InvalidOperationException("PART$09 state0 pressure check requires the live Link during preload.");
        if (!_initialized) UpdateFrame(new RoomEntityFrame(player,0,false),spawns);
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        // partCode09 calls state0, then falls through into state1 in the same dispatch.
        _initialized = true;

        byte tile = _room.GetMetatile(Position);
        if (TouchesLink(frame.Player))
        {
            if (frame.Player.ObjectZHigh != 0) return;
            if (!Reusable)
            {
                FinishOneShot(tile);
                return;
            }
            if (_pressed)
                return;
            SetButtonTile((byte)_data.PressedButtonTile);
            Press();
            return;
        }

        bool somethingOnButton = tile != _data.ButtonTile &&
            tile != _data.PressedButtonTile;
        if (somethingOnButton)
        {
            if (!Reusable) { FinishOneShot(tile); return; }
            if (_pressed)
                return;
            _releaseCounter = _data.ButtonObjectReleaseDelay;
            _room.SetUnderlyingMetatile(Position,(byte)_data.PressedButtonTile);
            Press();
            return;
        }

        if (_releaseCounter != 0)
        {
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


    private bool TouchesLink(Player player)
    {
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 button = OracleObjectMath.ToPixelPosition(Position);
        int radiusY = _data.ButtonRadiusY + (int)NpcCharacter.LinkCollisionRadius;
        int radiusX = _data.ButtonRadiusX + (int)NpcCharacter.LinkCollisionRadius;
        // checkObjectsCollided passes Link as object2: subtract button from
        // Link, add summed radii, then compare unsigned bytes. The accepted
        // interval includes -sum and excludes +sum, including coordinate wrap.
        return (((int)link.Y - (int)button.Y + radiusY) & 0xff) < ((radiusY * 2) & 0xff) &&
            (((int)link.X - (int)button.X + radiusX) & 0xff) < ((radiusX * 2) & 0xff);
    }

    private void Press()
    {
        _setTrigger(TriggerBit, true);
        _pressed = true;
        _playSound(_data.ButtonSound);
    }

    private void FinishOneShot(byte tile)
    {
        _room.SetUnderlyingMetatile(Position,(byte)_data.PressedButtonTile);
        if (tile == _data.ButtonTile) SetButtonTile((byte)_data.PressedButtonTile);
        Press();
        Finished = true;
    }

    private void SetButtonTile(byte tile) => _setTile((byte)PackedPosition,tile);
}
