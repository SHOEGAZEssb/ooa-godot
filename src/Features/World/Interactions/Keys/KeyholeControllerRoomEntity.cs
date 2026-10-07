using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscPuzzles_subid11/12/13 and interactiondc_subid01 retain their slot after scriptend (the
// caller ignores interactionRunScript's carry). Script/modal state belongs
// to the room event; this entity owns only source initialization and lifetime.
internal sealed class KeyholeControllerRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly KeyholeControllerRecord _record;
    private readonly OracleSaveData _save;
    private readonly RoomSession? _rooms;
    internal bool Initialized { get; private set; }
    public bool Finished { get; private set; }

    internal KeyholeControllerRoomEntity(KeyholeControllerRecord record, OracleSaveData save, RoomSession? rooms)
        : base(new Node2D { Name = $"KeyholeController_{record.Id:x2}_{record.SubId:x2}", Position = record.Position,
            Visible = false }, static _ => { })
    {
        _record = record;
        _save = save;
        _rooms = rooms;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Initialize();

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // Only miscPuzzles_subid11 tests returnIfScrollMode01Unset before
        // state0. Other handlers admit pending enabled$02 objects too.
        if (_record.Id == 0x90 && _record.SubId == 0x11 || Initialized || Finished) return;
        if (_rooms is null)
            throw new InvalidOperationException($"{_record.Source}: scrolling keyhole initialization requires active room identity.");
        Initialize();
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // $11's returnIfScrollMode01Unset defers state0 until ordinary room
        // updates resume; $12/$13 initialize during destination preload.
        if (_record.Id != 0x90 || _record.SubId != 0x11) Initialize();
        return ScreenTransitionPresentation.Hidden;
    }

    private void Initialize()
    {
        if (Initialized || Finished) return;
        // bank0.getThisRoomFlags samples wActiveGroup/wActiveRoom, which
        // already identify the destination when outgoing state0 runs.
        if (_save.HasRoomFlag(_rooms?.ActiveGroup ?? _record.Group,
            _rooms?.CurrentRoom.Id ?? _record.Room, OracleSaveData.RoomFlag80))
            Finished = true;
        else
            Initialized = true;
    }
}
