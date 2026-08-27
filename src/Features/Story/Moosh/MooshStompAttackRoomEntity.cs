using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ITEM_28's Moosh form: a 24-by-24-radius collision centered sixteen pixels
/// below Link, alive for $14 original updates and probing its 3x3 tile grid.
/// </summary>
internal sealed partial class MooshStompAttackRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private static readonly Vector2[] BreakOffsets =
    [
        Vector2.Zero,
        new(-16, -16), new(0, -16), new(16, -16),
        new(-16, 0), new(16, 0),
        new(-16, 16), new(0, 16), new(16, 16)
    ];

    private readonly int _group;
    private readonly OracleRoomData _room;
    private readonly BreakableTileDatabase _breakables;
    private readonly OracleSaveData? _saveData;
    private readonly Func<Vector2I, int?>? _linkedRoomNeighbor;
    private readonly Action<Rect2, int, int, int> _applyHit;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly Func<int, int?> _decideBreakableDrop;
    private int _counter = 0x14;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal MooshStompAttackRoomEntity(
        MooshStompAttackSpawn spawn,
        OracleRoomData room,
        BreakableTileDatabase breakables,
        OracleSaveData? saveData,
        Func<Vector2I, int?>? linkedRoomNeighbor,
        Action<Rect2, int, int, int> applyHit,
        Action roomTileChanged,
        Func<long> animationTick,
        Action<int> playSound,
        Func<int, int?> decideBreakableDrop)
    {
        _group = spawn.Group;
        _room = room;
        _breakables = breakables;
        _saveData = saveData;
        _linkedRoomNeighbor = linkedRoomNeighbor;
        _applyHit = applyHit;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _playSound = playSound;
        _decideBreakableDrop = decideBreakableDrop;
        Position = spawn.Position;
        Name = "MooshStompAttack";
        Visible = false;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        if (Finished)
            return;

        var hitbox = new Rect2(
            Position - new Vector2(24, 24),
            new Vector2(48, 48));
        _applyHit(hitbox, 0, 7, 7);
        foreach (Vector2 offset in BreakOffsets)
            TryBreakTile(Position + offset, spawns);
        if (--_counter == 0)
            Finished = true;
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => _ = offset;

    private void TryBreakTile(
        Vector2 point,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
        {
            return;
        }
        if (_breakables.TryBreak(
                _room,
                BreakableTileDatabase.SourceMooshButtstomp,
                point,
                _saveData,
                _group,
                _animationTick,
                _linkedRoomNeighbor,
                out BreakableTileBreak result) !=
            BreakableTileBreakStatus.Broken)
        {
            return;
        }
        result.ApplyCommonEffects(
            _playSound, _decideBreakableDrop, spawns);
        if (BreakableTileEffectSpawn.Create(
                _room, result.TileCenter, result.Record.Effect) is { } effect)
        {
            spawns.Add(effect);
        }
        _roomTileChanged();
    }
}
