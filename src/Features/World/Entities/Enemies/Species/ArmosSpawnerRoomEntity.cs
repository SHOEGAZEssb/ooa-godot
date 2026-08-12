using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Spawner form of ENEMY_ARMOS $1d with bit 7 of subid clear.</summary>
internal sealed partial class ArmosSpawnerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    private readonly OracleRoomData _room;
    private readonly int _sourceTile;
    private readonly int _replacementTile;
    private readonly ArmosBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Armos;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => !Finished;

    internal ArmosSpawnerRoomEntity(
        OracleRoomData room,
        int sourceTile,
        int replacementTile)
    {
        _room = room;
        _sourceTile = sourceTile;
        _replacementTile = replacementTile;
        Name = $"ArmosSpawner_{sourceTile:x2}_{replacementTile:x2}";
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        for (int y = 0; y < _room.HeightInTiles; y++)
        for (int x = 0; x < _room.WidthInTiles; x++)
        {
            Vector2 center = new(
                x * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            if (_room.GetMetatile(center) != _sourceTile)
                continue;
            spawns.Add(new ArmosSpawn(
                new Vector2(
                    center.X,
                    y * OracleRoomData.MetatileSize + _behavior.SpawnYOffset),
                _replacementTile));
        }
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
