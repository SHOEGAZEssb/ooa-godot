using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Permanent PART_LIGHTABLE_TORCH $06:$00. A seed collision selects state 2;
/// the following object update increments the room count, plays SND_LIGHTTORCH,
/// attempts setTile $09, and deletes even when the tile queue is full.
/// </summary>
internal sealed partial class LightableTorchRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, ISeedHittableRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly LightableTorchState _state;
    private readonly DarkRoomDatabase _data;
    private readonly Action<int> _playSound;
    private readonly Action<byte,byte> _setTile;
    private bool _initialized;
    private bool _hit;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal bool Initialized => _initialized;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int PackedPosition { get; }
    internal bool HitPending => _hit;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_data.TorchRadiusX, _data.TorchRadiusY),
        new Vector2(_data.TorchRadiusX * 2, _data.TorchRadiusY * 2));

    internal LightableTorchRoomEntity(
        LightableTorchState state,
        int packedPosition,
        DarkRoomDatabase data,
        Action<int> playSound,
        Action<byte,byte> setTile)
    {
        _state = state;
        _data = data;
        _playSound = playSound;
        _setTile = setTile;
        PackedPosition = packedPosition;
        Position = PositionFromPacked(packedPosition);
        Name = $"LightableTorch_{packedPosition:x2}";
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        _initialized = true;
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            return;
        }
        if (!_hit || Finished)
            return;

        _state.IncrementLitCount();
        _playSound(_data.LightSound);
        _setTile((byte)PackedPosition,(byte)_data.LitTile);
        Finished = true;
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem != ItemId.EmberSeed || !_initialized || _hit || Finished ||
            !hitbox.Intersects(CollisionBounds))
            return SeedHitResult.None;
        _hit = true;
        return SeedHitResult.Consume;
    }


    private static Vector2 PositionFromPacked(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
