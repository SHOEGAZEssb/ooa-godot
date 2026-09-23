using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX $c7:$08 from
/// objectData_makeAllTorchesLightable. It scans the full 176-byte room layout
/// in address order, creates PART_LIGHTABLE_TORCH $06:$00 on every tile $08,
/// then deletes itself.
/// </summary>
internal sealed partial class LightableTorchScannerRoomEntity :
    DungeonMechanicRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly LightableTorchState _state;
    private readonly DarkRoomDatabase _data;
    private readonly Func<LightableTorchState,int,bool> _createTorch;

    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !Finished;
    public bool UpdatesDuringRoomEntityFreeze => !Finished;

    internal LightableTorchScannerRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        LightableTorchState state,
        DarkRoomDatabase data,
        Func<LightableTorchState,int,bool> createTorch)
        : base(record, $"LightableTorchScanner_{record.Order}")
    {
        if (record is not
            { Id: 0xc7, SubId: 0x08, PackedPosition: 0x06, Parameter: 0x10 })
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _room = room;
        _state = state;
        _data = data;
        _createTorch = createTorch;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Scan();
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Scan();

    private void Scan()
    {
        if (Finished)
            return;
        if (_room.Layout.Length != 176)
        {
            throw new InvalidOperationException(
                $"INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX $c7:$08 in room " +
                $"{_record.Group:x1}:{_record.Room:x2} requires the original " +
                "176-byte large-room layout.");
        }

        int count = 0;
        for (int index = 0; index < _room.Layout.Length; index++)
        {
            if (_room.Layout[index] != _data.UnlitTile)
                continue;
            int packedPosition = (index / 16 << 4) | index % 16;
            // interactionCodec7 continues the scan even when getFreePartSlot fails.
            _ = _createTorch(_state, packedPosition);
            count++;
        }
        _state.SetTotalTorches(count);
        Finished = true;
    }
}
