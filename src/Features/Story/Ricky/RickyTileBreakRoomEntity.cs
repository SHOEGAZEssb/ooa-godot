using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// One ordered tryToBreakTile call emitted by Ricky's movement or landing
/// state. Keeping it as a spawn lets the room entity factory bind the active
/// destination room after a companion-owned screen transition.
/// </summary>
internal sealed partial class RickyTileBreakRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly RickyTileBreakSpawn _spawn;
    private readonly RickyAttackTileBreaker _breaker;

    internal RickyTileBreakRoomEntity(
        RickyTileBreakSpawn spawn,
        RickyAttackTileBreaker breaker)
    {
        _spawn = spawn;
        _breaker = breaker;
        Position = spawn.Position;
        Name = $"RickyTileBreak_{spawn.Source:x2}";
    }

    public Node2D Node => this;
    public bool Finished { get; private set; }

    public void SetTransitionDrawOffset(Vector2 offset) =>
        Position = _spawn.Position + offset;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _breaker.TryBreak(_spawn.Position, _spawn.Source, spawns);
        Finished = true;
    }
}

internal sealed record RickyTileBreakSpawn(
    Vector2 Position,
    int Source,
    int Group,
    int Room) : RoomEntitySpawn;
