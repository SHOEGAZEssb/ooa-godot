using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Spawner form of ENEMY_ARMOS $1d with bit 7 of subid clear.</summary>
internal sealed partial class ArmosSpawnerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly int _sourceTile;
    private readonly int _replacementTile;
    private readonly OracleRandom _random;
    private readonly System.Func<int, bool> _slotsAvailable;
    internal int State { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    private readonly ArmosBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Armos;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => !Finished;

    internal ArmosSpawnerRoomEntity(
        OracleRoomData room,
        int sourceTile,
        int replacementTile, OracleRandom random, System.Func<int, bool> slotsAvailable)
    {
        _room = room;
        _sourceTile = sourceTile;
        _replacementTile = replacementTile;
        _random = random;
        _slotsAvailable = slotsAvailable;
        Name = $"ArmosSpawner_{sourceTile:x2}_{replacementTile:x2}";
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (State == 0)
        {
            _random.Next(); // enemyStandardUpdate before armos_uninitialized.
            State = 1;
            return;
        }
        int requested = 0;
        for (int y = 0; y < _room.HeightInTiles; y++)
        for (int x = 0; x < _room.WidthInTiles; x++)
        {
            Vector2 center = new(
                x * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            if (_room.GetMetatile(center) != _sourceTile)
                continue;
            if (!_slotsAvailable(requested + 1)) continue;
            requested++;
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
