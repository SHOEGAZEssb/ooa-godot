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
        Func<long> animationTick)
    {
        _group = spawn.Group;
        _room = room;
        _breakables = breakables;
        _saveData = saveData;
        _linkedRoomNeighbor = linkedRoomNeighbor;
        _applyHit = applyHit;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Position = spawn.Position;
        Name = "MooshStompAttack";
        Visible = false;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        if (Finished)
            return;

        var hitbox = new Rect2(
            Position - new Vector2(24, 24),
            new Vector2(48, 48));
        _applyHit(hitbox, 0, 7, 7);
        foreach (Vector2 offset in BreakOffsets)
            TryBreakTile(Position + offset);
        if (--_counter == 0)
            Finished = true;
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => _ = offset;

    private void TryBreakTile(Vector2 point)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
        {
            return;
        }
        byte tile = _room.GetMetatile(point);
        if (!_breakables.TryGet(
                _room.ActiveCollisions,
                tile,
                out BreakableTileRecord breakable) ||
            !breakable.AllowsSource(
                BreakableTileDatabase.SourceMooshButtstomp))
        {
            return;
        }

        int packed = _room.GetPackedPosition(point);
        Vector2 tileCenter = new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        byte replacement = breakable.ReplacementFor(_room, tileCenter);
        bool changed = breakable.Replacement == 0 ||
            _room.ReplaceMetatile(
                tileCenter, tile, replacement, _animationTick());
        if (!changed)
            return;
        breakable.ApplyPersistentEffects(
            _saveData,
            _group,
            _room.Id,
            _linkedRoomNeighbor);
        _roomTileChanged();
    }
}
