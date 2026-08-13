using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DUNGEON_EVENTS $21:$0a/$0c. The orb variant converts toggle bit
/// $10 into a dynamic chest/Armos pair. The button variant copies the active
/// trigger byte to the Armos trigger and creates the falling-key watcher
/// before the shared Armos spawner.
/// </summary>
internal sealed partial class MoonlitGrottoArmosEventRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<int> _triggerState;
    private readonly GroundTreasureGrantRequest? _buttonKey;
    private bool _initialized;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal bool Initialized => _initialized;

    internal MoonlitGrottoArmosEventRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        OracleSaveData? save,
        OracleRuntimeState runtime,
        Func<int> triggerState,
        GroundTreasureGrantRequest? buttonKey)
    {
        if (record.Id != 0x21 || record.SubId is not (0x0a or 0x0c) ||
            record.SubId == 0x0a && buttonKey is not null ||
            record.SubId == 0x0c && buttonKey is null)
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _data = data;
        _save = save;
        _runtime = runtime;
        _triggerState = triggerState;
        _buttonKey = buttonKey;
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

        int armosTrigger;
        if (_record.SubId == 0x0a)
        {
            if ((_runtime.ReadWramByte(
                    OracleRuntimeState.ToggleBlocksStateAddress) &
                 _data.MoonlitOrbMask) == 0)
            {
                return;
            }
            armosTrigger = 1;
        }
        else
        {
            armosTrigger = _triggerState();
            if (armosTrigger == 0)
                return;
        }

        _runtime.SetWramByte(
            OracleRuntimeState.ArmosTriggerAddress,
            checked((byte)armosTrigger));
        // parseGivenObjectData creates this interaction before the enemy
        // spawner. Neither receives an update until the full spawn queue is
        // materialized, so wNumEnemies already includes the Armos.
        if (_record.SubId == 0x0a)
        {
            spawns.Add(new EnemyClearChestSpawn(
                _record.Group,
                _record.Room,
                _data.MoonlitArmosChestPosition));
        }
        else
        {
            spawns.Add(new EnemySmallKeyRewardSpawn(_buttonKey!.Value));
        }
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
        if (_record.SubId != 0x0a)
            return;
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
