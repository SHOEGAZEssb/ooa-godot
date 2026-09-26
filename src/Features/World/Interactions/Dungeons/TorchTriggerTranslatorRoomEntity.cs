using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_TRIGGER_TRANSLATOR $24:$02. Every interaction update mirrors the
/// exact wNumTorchesLit comparison into the specified wActiveTriggers mask.
/// </summary>
internal sealed partial class TorchTriggerTranslatorRoomEntity :
    DungeonMechanicRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly LightableTorchState _state;
    private readonly Action<int, bool> _setTrigger;
    private readonly int _requiredCount;
    private readonly Func<IRoomEntity,bool> _isOutgoing;

    public bool Finished { get; private set; }
    // interactionCode24 never increments state, so the native state0 gates
    // permit every dispatch even under text/scroll/disabled interactions.
    public bool UpdatesDuringDialogue => !Finished;
    public bool UpdatesDuringRoomEntityFreeze => !Finished;

    internal int TriggerBit { get; }
    internal int RequiredCount => _requiredCount;

    internal TorchTriggerTranslatorRoomEntity(
        DungeonMechanicDatabaseRecord record,
        LightableTorchState state,
        Action<int, bool> setTrigger,
        Func<IRoomEntity,bool> isOutgoing)
        : base(record, $"TorchTriggerTranslator_{record.Order}")
    {
        if (record.Id != InteractionId.TriggerTranslator || record.SubId != 0x02 ||
            record.Predicate != TriggerPredicate.Exact)
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        TriggerBit = record.Parameter switch
        {
            0x01 => 0,
            0x02 => 1,
            0x04 => 2,
            0x08 => 3,
            0x10 => 4,
            0x20 => 5,
            0x40 => 6,
            0x80 => 7,
            _ => throw new ArgumentOutOfRangeException(
                nameof(record), "The original trigger mask must contain one bit.")
        };
        _requiredCount = record.PackedPosition;
        _state = state;
        _setTrigger = setTrigger;
        _isOutgoing = isOutgoing;
        Visible = false;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Dispatch();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Dispatch();
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame = default) => Dispatch();

    private void Dispatch()
    {
        if (Finished) return;
        // interactionDeleteAndRetIfEnabled02 precedes the trigger write.
        if (_isOutgoing(this)) { Finished = true; return; }
        _setTrigger(TriggerBit, _state.LitCount == _requiredCount);
    }
}
