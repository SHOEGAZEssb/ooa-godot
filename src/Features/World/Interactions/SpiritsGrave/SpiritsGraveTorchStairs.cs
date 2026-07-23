using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Native D1 two-torch staircase script in room $4:$1b.</summary>
internal sealed partial class SpiritsGraveTorchStairs : Node2D,
    IRoomEntity, IFixedRoomEntity, ISeedHittableRoomEntity, IRoomEntityLifetime
{
    private readonly ObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData? _save;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly List<int> _unlit = new();
    private int _pending = -1;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int LitCount { get; private set; }
    internal IReadOnlyList<int> UnlitPositions => _unlit;

    internal SpiritsGraveTorchStairs(
        ObjectRecord record,
        OracleRoomData room,
        OracleSaveData? save,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _save = save;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = "SpiritsGraveTorchStairs";
        Position = record.Position;
        for (int index = 0; index < room.Layout.Length; index++)
        {
            if (room.Layout[index] == 0x08)
                _unlit.Add((index / 16 << 4) | index % 16);
        }
        if (_unlit.Count != 2)
            throw new InvalidOperationException(
                $"{record.Source} expected two unlit torches, found {_unlit.Count}.");
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_pending >= 0 || Finished)
            return SeedHitResult.None;
        foreach (int packed in _unlit)
        {
            Vector2 center = PositionFromPacked(packed);
            if (hitbox.Intersects(new Rect2(center - new Vector2(6, 6), new Vector2(12, 12))))
            {
                _pending = packed;
                return SeedHitResult.Consume;
            }
        }
        return SeedHitResult.None;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_pending < 0 || Finished)
            return;
        int packed = _pending;
        _pending = -1;
        _unlit.Remove(packed);
        _room.SetPositionTileAndCollision(
            PositionFromPacked(packed), 0x09, null, _animationTick());
        _roomTileChanged();
        _playSound(OracleSoundEngine.SndLightTorch);
        LitCount++;
        if (LitCount != 2)
            return;

        _save?.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80);
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        spawns.Add(new PuzzlePuffSpawn(_record.Position, 0));
        _room.SetPositionTileAndCollision(
            _record.Position, 0x45, null, _animationTick());
        _roomTileChanged();
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public void SetTransitionDrawOffset(Vector2 offset) { }

    private static Vector2 PositionFromPacked(int packed) => new(
        (packed & 0x0f) * 16 + 8,
        (packed >> 4) * 16 + 8);
}
