using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$11/$12, spawnChestAndDeleteSelf.</summary>
internal sealed partial class DungeonPuzzleChestRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly Func<bool> _conditionMet;
    private readonly OracleSaveData? _saveData;
    private readonly int _chestTile;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DungeonPuzzleChestRoomEntity(DungeonObjectRecord record, OracleRoomData room,
        Func<bool> conditionMet, OracleSaveData? saveData, int chestTile,
        Action<int> playSound, Action roomTileChanged, Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _conditionMet = conditionMet;
        _saveData = saveData;
        _chestTile = chestTile;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Position = record.Position;
        Name = "DungeonPuzzleChest";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_saveData?.HasRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlagItem) == true)
            Finished = true;
        if (Finished || !_conditionMet())
            return;
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        _room.SetPositionTileAndCollision(Position, (byte)_chestTile, null, _animationTick());
        _roomTileChanged();
        spawns.Add(new PuzzlePuffSpawn(Position, OracleSoundEngine.SndPoof));
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
