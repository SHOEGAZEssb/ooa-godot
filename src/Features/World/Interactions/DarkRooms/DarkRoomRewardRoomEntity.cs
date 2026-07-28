using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_MISCELLANEOUS_2 $dc:$00. Its ROOMFLAG_ITEM predicate is checked
/// before the exact two-torch count, and it deletes itself after creating the
/// falling Graveyard Key interaction.
/// </summary>
internal sealed partial class DarkRoomRewardRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DarkRoomDatabaseRecord _record;
    private readonly DarkRoomDatabase _data;
    private readonly DarkRoomState _state;
    private readonly OracleSaveData? _save;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DarkRoomRewardRoomEntity(
        DarkRoomDatabaseRecord record,
        DarkRoomDatabase data,
        DarkRoomState state,
        OracleSaveData? save)
    {
        if (record is not
            { Kind: DarkRoomDatabaseObjectKind.Reward, Id: 0xdc, SubId: 0x00 })
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _data = data;
        _state = state;
        _save = save;
        Name = $"DarkRoomReward_{record.Order}";
        Position = new Vector2(record.X, record.Y);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_save?.HasRoomFlag(
            _record.Group, _record.Room, OracleSaveData.RoomFlagItem) == true)
        {
            Finished = true;
            return;
        }
        if (_state.LitCount != _record.RequiredCount)
            return;

        spawns.Add(new GroundTreasureGrantSpawn(new GroundTreasureGrantRequest(
            _record.Group,
            _record.Room,
            _record.Order,
            _record.Y,
            _record.X,
            _record.TreasureObject,
            _record.Source)
        {
            SpawnMode = _data.RewardSpawnMode,
            GrabMode = _data.RewardGrabMode,
            SpawnDelayFrames = _data.SpawnDelay,
            InitialZPixels = _data.AboveScreenFallback,
            BounceCount = _data.BounceCount,
            Gravity = _data.Gravity,
            BounceSpeed = _data.BounceSpeed,
            SpawnSound = _data.SpawnSound,
            LandingSound = _data.LandingSound,
            InitialZAboveScreen = true,
            AboveScreenMargin = _data.AboveScreenMargin,
            AboveScreenFallback = _data.AboveScreenFallback
        }));
        Finished = true;
    }

}
