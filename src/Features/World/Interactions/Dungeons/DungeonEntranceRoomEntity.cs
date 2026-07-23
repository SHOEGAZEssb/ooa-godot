using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Invisible INTERAC_DUNGEON_STUFF $12:$00.</summary>
internal sealed class DungeonEntranceRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly EntryRecord _record;
    private readonly DungeonEntranceInteractionDatabase _data;
    private readonly OracleRuntimeState _runtimeState;
    private readonly Action<int, string> _triggered;
    private readonly bool _whiteoutEntry;
    private bool _initialized;

    internal DungeonEntranceRoomEntity(
        Vector2 position,
        EntryRecord record,
        DungeonEntranceInteractionDatabase data,
        OracleRuntimeState runtimeState,
        bool whiteoutEntry,
        Action<int, string> triggered)
        : base(new Node2D { Name = "DungeonEntrance", Visible = false }, static _ => { })
    {
        Entity.Position = position;
        _record = record;
        _data = data;
        _runtimeState = runtimeState;
        _whiteoutEntry = whiteoutEntry;
        _triggered = triggered;
    }

    public bool Finished { get; private set; }
    internal bool Initialized => _initialized;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            if (!_whiteoutEntry || frame.Player.Position.Y < _data.EntryMinimumY)
            {
                Finished = true;
                return;
            }

            // initializeDungeonStuff clears these three session-persistent
            // dungeon fields before the Ages table supplies wSpinnerState.
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
            _runtimeState.SetWramByte(
                OracleRuntimeState.SpinnerStateAddress, (byte)_record.SpinnerState);
        }

        Vector2 delta = frame.Player.Position - Entity.Position;
        float radius = _data.EntryRadius + NpcCharacter.LinkCollisionRadius;
        if (Mathf.Abs(delta.X) >= radius || Mathf.Abs(delta.Y) >= radius)
            return;
        Finished = true;
        _triggered(_record.TextId, _record.Message);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
