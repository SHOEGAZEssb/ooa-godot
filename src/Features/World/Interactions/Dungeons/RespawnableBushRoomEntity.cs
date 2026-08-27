using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_RESPAWNABLE_BUSH $0f. Sword/spin and bomb hits cut it, consume one
/// global RNG value for the fixed 50% drop chance, then regenerate on the
/// original half-rate $f0 plus $0c/$08 update sequence.
/// </summary>
internal sealed partial class RespawnableBushRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    ISwordHittableRoomEntity, IItemCollisionHittableRoomEntity,
    IObjectCollisionHeightRoomEntity
{
    private readonly int _packedPosition;
    private readonly int _dropSubId;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRandom _random;
    private readonly Func<long> _animationTick;
    private readonly Action _roomTileChanged;
    private bool _initialized;
    private RespawnableBushState _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished => false;
    public int CollisionZ => 0;
    internal int PackedPosition => _packedPosition;
    internal int DropSubId => _dropSubId;
    internal RespawnableBushState State => _state;
    internal int Counter => _counter;
    internal bool CollisionEnabled => _initialized &&
        _state == RespawnableBushState.Ready;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(
            _data.RespawningBushRadiusX,
            _data.RespawningBushRadiusY),
        new Vector2(
            _data.RespawningBushRadiusX * 2,
            _data.RespawningBushRadiusY * 2));

    internal RespawnableBushRoomEntity(
        int packedPosition,
        int dropSubId,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        OracleRandom random,
        Func<long> animationTick,
        Action roomTileChanged)
    {
        if (packedPosition is < 0 or > 0xaf ||
            !ItemDropDatabase.IsRuntimeSupported(dropSubId))
        {
            throw new ArgumentOutOfRangeException(nameof(packedPosition));
        }
        _packedPosition = packedPosition;
        _dropSubId = dropSubId;
        _room = room;
        _data = data;
        _random = random;
        _animationTick = animationTick;
        _roomTileChanged = roomTileChanged;
        Position = Point(packedPosition);
        Name = $"RespawnableBush_{packedPosition:x2}_{dropSubId:x2}";
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            _state = RespawnableBushState.Ready;
            return;
        }

        switch (_state)
        {
            case RespawnableBushState.Ready:
                return;
            case RespawnableBushState.CutDelay:
                if ((frame.Counter & 1) == 0)
                    return;
                _counter--;
                if (_counter != 0)
                    return;
                _counter = _data.RespawningBushRegenWait;
                _state = RespawnableBushState.Regenerating;
                SetTile(_data.RespawningBushRegenTile);
                return;
            case RespawnableBushState.Regenerating:
                _counter--;
                if (_counter != 0)
                    return;
                _counter = _data.RespawningBushReadyWait;
                _state = RespawnableBushState.Arming;
                SetTile(_data.RespawningBushReadyTile);
                return;
            case RespawnableBushState.Arming:
                _counter--;
                if (_counter == 0)
                    _state = RespawnableBushState.Ready;
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) => Cut(hitbox, spawns);

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) =>
        collision == RoomEntityItemCollision.Bomb && Cut(hitbox, spawns);

    private bool Cut(Rect2 hitbox, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CollisionEnabled || !hitbox.Intersects(CollisionBounds))
            return false;
        _state = RespawnableBushState.CutDelay;
        _counter = _data.RespawningBushDelay;
        SetTile(_data.RespawningBushCutTile);

        if ((_random.Next().Value & 0x01) != 0)
            spawns.Add(new ItemDropSpawn(_dropSubId, Position));
        spawns.Add(new GrassDebrisSpawn(Position));
        return true;
    }

    private void SetTile(int tile)
    {
        _room.SetPositionTileAndCollision(
            Position, (byte)tile, null, _animationTick());
        _roomTileChanged();
    }

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}

internal enum RespawnableBushState
{
    Ready,
    CutDelay,
    Regenerating,
    Arming
}
