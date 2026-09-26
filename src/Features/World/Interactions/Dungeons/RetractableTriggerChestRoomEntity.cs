using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC$21:$17 retains state0 and mirrors exact wActiveTriggers equality.</summary>
internal sealed partial class RetractableTriggerChestRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _triggers;
    private readonly Func<bool> _itemFlag;
    private readonly Func<IRoomEntity,bool> _outgoing;
    private readonly Action<byte,byte> _setTile;
    private readonly Func<Vector2,bool> _puff;
    private readonly Action<int> _sound;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !Finished;
    public bool UpdatesDuringRoomEntityFreeze => !Finished;
    internal int PackedPosition => _record.PackedPosition;

    internal RetractableTriggerChestRoomEntity(DungeonMechanicDatabaseRecord record,OracleRoomData room,
        DungeonMechanicDatabase data,Func<int> triggers,Func<bool> itemFlag,Func<IRoomEntity,bool> outgoing,
        Action<byte,byte> setTile,Func<Vector2,bool> puff,Action<int> sound) : base(record,$"RetractableChest_{record.Order}")
    {
        if(record.Id!=InteractionId.DungeonEvents || record.SubId!=0x17 || record.Predicate!=TriggerPredicate.Exact)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record=record; _room=room; _data=data; _triggers=triggers; _itemFlag=itemFlag;
        _outgoing=outgoing; _setTile=setTile; _puff=puff; _sound=sound; Visible=false;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return ScreenTransitionPresentation.Hidden; }
    private void Dispatch()
    {
        if(Finished) return;
        if(_outgoing(this) || _itemFlag()) { Finished=true; return; }
        bool active = _triggers()==_record.Parameter;
        bool chest = _room.GetMetatile(Position)==_data.ChestTile;
        if(active==chest) return;
        // Source reads the live w3RoomLayoutBuffer on every retraction. Neither
        // setTile nor createPuffAt failure cancels the remaining side effects.
        _setTile((byte)PackedPosition,active ? (byte)_data.ChestTile : _room.GetUnderlyingMetatile(Position));
        _ = _puff(Position);
        if(active) _sound(_data.SolveSound);
    }
}
