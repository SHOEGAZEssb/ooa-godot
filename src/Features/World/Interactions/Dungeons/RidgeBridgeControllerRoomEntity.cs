using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_2 $dc:$0c/$0d, Rolling Ridge button bridges.</summary>
internal sealed partial class RidgeBridgeControllerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleSaveData _save;
    private readonly Func<int> _triggers;
    private readonly Func<bool> _freePartSlot;
    private readonly Action<int> _sound;
    private readonly DungeonMechanicDatabase _data;
    private bool _initialized;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal RidgeBridgeControllerRoomEntity(DungeonMechanicDatabaseRecord record, OracleSaveData save,
        Func<int> triggers, Func<bool> freePartSlot, Action<int> sound, DungeonMechanicDatabase data)
        : base(record, $"RidgeBridge_dc_{record.SubId:x2}")
    {
        _record = record; _save = save; _triggers = triggers;
        _freePartSlot = freePartSlot; _sound = sound; _data = data;
    }
    private void Initialize()
    {
        _initialized = true;
        Finished = _save.HasRoomFlag(_record.Group, _record.Room, 0x80);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized) { Initialize(); return; }
        // Parts (including the button) publish wActiveTriggers before this
        // interaction. Any bit suffices; allocation failure retries next tick.
        if (_triggers() == 0 || !_freePartSlot()) return;
        spawns.Add(new BridgeSpawnerSpawn(_record.PackedPosition, _record.Parameter,
            _data.RidgeBridgeAngle(_record.SubId)));
        _save.SetRoomFlag(_record.Group, _record.Room, 0x80);
        _sound(_data.SolveSound);
        Finished = true;
    }
}
