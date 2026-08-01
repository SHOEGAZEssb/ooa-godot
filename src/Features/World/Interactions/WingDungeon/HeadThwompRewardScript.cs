using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Dungeon $02's specialized wingDungeonScript_bossDeath. The first clear
/// restores two bottom staircase cells across consecutive createpuff command
/// boundaries, then creates the Heart Container at its script-owned position.
/// </summary>
internal sealed partial class HeadThwompRewardScript : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonObjectRecord _record;
    private readonly DungeonBossRewardScriptDefinition _definition;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData? _save;
    private readonly Func<int> _enemyCount;
    private readonly GroundTreasureGrantRequest _treasure;
    private readonly Action _enableLinkCollisionsAndMenu;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private int _stage;

    internal HeadThwompRewardScript(
        DungeonObjectRecord record,
        DungeonBossRewardScriptDefinition definition,
        OracleRoomData room,
        OracleSaveData? save,
        Func<int> enemyCount,
        GroundTreasureGrantRequest treasure,
        Action enableLinkCollisionsAndMenu,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        if (record.Group != definition.Group ||
            record.Room != definition.Room ||
            definition.StairPositions is not [_, _] ||
            treasure.Group != definition.Group ||
            treasure.Room != definition.Room ||
            treasure.Y != definition.RewardY ||
            treasure.X != definition.RewardX)
        {
            throw new InvalidOperationException(
                $"{definition.Source} does not match its boss-room placement/reward.");
        }

        _record = record;
        _definition = definition;
        _room = room;
        _save = save;
        _enemyCount = enemyCount;
        _treasure = treasure;
        _enableLinkCollisionsAndMenu = enableLinkCollisionsAndMenu;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = "HeadThwompRewardScript";
        Position = record.Position;
    }

    public Node2D Node => this;
    public bool Finished { get; private set; }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;

        if (_stage == 0)
        {
            // jumpifroomflagset precedes checknoenemies: completed-room
            // re-entry restores an uncollected container immediately and
            // does not replay the staircase puffs.
            if (_save?.HasRoomFlag(
                    _record.Group,
                    _record.Room,
                    OracleSaveData.RoomFlag80) == true)
            {
                SpawnTreasure(spawns);
                return;
            }
            if (_enemyCount() != 0)
                return;

            _save?.SetRoomFlag(
                _record.Group,
                _record.Room,
                OracleSaveData.RoomFlag80);
            SpawnStairPuff(spawns, 0);
            _stage = 1;
            return;
        }

        SetStairTile(_stage - 1);
        if (_stage < _definition.StairPositions.Length)
        {
            SpawnStairPuff(spawns, _stage);
            _stage++;
            return;
        }
        SpawnTreasure(spawns);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void SpawnStairPuff(
        ICollection<RoomEntitySpawn> spawns,
        int index) =>
        spawns.Add(new PuzzlePuffSpawn(
            PositionFromPacked(_definition.StairPositions[index]),
            OracleSoundEngine.SndPoof));

    private void SetStairTile(int index)
    {
        _room.SetPositionTileAndCollision(
            PositionFromPacked(_definition.StairPositions[index]),
            (byte)_definition.StairTile,
            collision: null,
            _animationTick());
        _roomTileChanged();
    }

    private void SpawnTreasure(ICollection<RoomEntitySpawn> spawns)
    {
        spawns.Add(new GroundTreasureGrantSpawn(_treasure));
        _enableLinkCollisionsAndMenu();
        Finished = true;
    }

    private static Vector2 PositionFromPacked(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
