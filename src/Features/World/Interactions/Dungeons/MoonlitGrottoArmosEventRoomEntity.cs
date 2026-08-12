using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DUNGEON_EVENTS $21:$0a. It creates the orb before testing the
/// item flag, then converts toggle bit $10 into the dynamic chest/Armos pair.
/// </summary>
internal sealed partial class MoonlitGrottoArmosEventRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly OracleRuntimeState _runtime;
    private bool _initialized;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal bool Initialized => _initialized;

    internal MoonlitGrottoArmosEventRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        OracleSaveData? save,
        OracleRuntimeState runtime)
    {
        if (record.Id != 0x21 || record.SubId != 0x0a)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _data = data;
        _save = save;
        _runtime = runtime;
        Name = $"MoonlitArmosEvent_{record.Room:x2}";
        Visible = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Initialize(spawns);
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            Initialize(spawns);
            return;
        }

        if ((_runtime.ReadWramByte(
                OracleRuntimeState.ToggleBlocksStateAddress) &
             _data.MoonlitOrbMask) == 0)
        {
            return;
        }

        _runtime.SetWramByte(OracleRuntimeState.ArmosTriggerAddress, 1);
        // parseGivenObjectData creates this interaction before the enemy
        // spawner. Neither receives an update until the full spawn queue is
        // materialized, so wNumEnemies already includes the Armos.
        spawns.Add(new EnemyClearChestSpawn(
            _record.Group,
            _record.Room,
            _data.MoonlitArmosChestPosition));
        spawns.Add(new ArmosSpawnerSpawn(
            _data.MoonlitArmosSourceTile,
            _data.MoonlitArmosReplacementTile));
        Finished = true;
    }

    private void Initialize(ICollection<RoomEntitySpawn> spawns)
    {
        if (_initialized)
            return;
        _initialized = true;
        byte state = _runtime.ReadWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress);
        _runtime.SetWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress,
            (byte)(state & ~_data.MoonlitOrbMask));
        spawns.Add(new MoonlitGrottoOrbSpawn(
            _record.Group, _record.Room));
        if (_save?.HasRoomFlag(
                _record.Group,
                _record.Room,
                OracleSaveData.RoomFlagItem) == true)
        {
            Finished = true;
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
