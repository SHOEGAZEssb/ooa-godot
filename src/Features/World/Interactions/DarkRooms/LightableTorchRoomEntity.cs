using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Permanent PART_LIGHTABLE_TORCH $06:$00. A seed collision selects state 2;
/// the following object update increments the room count, changes tile $08 to
/// $09, plays SND_LIGHTTORCH, and deletes the part.
/// </summary>
internal sealed partial class LightableTorchRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, ISeedHittableRoomEntity, IRoomEntityLifetime
{
    private readonly DarkRoomState _state;
    private readonly OracleRoomData _room;
    private readonly DarkRoomDatabase _data;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private bool _initialized;
    private bool _hit;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int PackedPosition { get; }
    internal bool HitPending => _hit;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_data.TorchRadiusX, _data.TorchRadiusY),
        new Vector2(_data.TorchRadiusX * 2, _data.TorchRadiusY * 2));

    internal LightableTorchRoomEntity(
        DarkRoomState state,
        int packedPosition,
        OracleRoomData room,
        DarkRoomDatabase data,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _state = state;
        _room = room;
        _data = data;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        PackedPosition = packedPosition;
        Position = PositionFromPacked(packedPosition);
        Name = $"LightableTorch_{packedPosition:x2}";
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

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
        _room.SetPositionTileAndCollision(
            Position, (byte)_data.LitTile, null, _animationTick());
        _roomTileChanged();
        Finished = true;
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized || _hit || Finished || !hitbox.Intersects(CollisionBounds))
            return SeedHitResult.None;
        _hit = true;
        return SeedHitResult.Consume;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private static Vector2 PositionFromPacked(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
