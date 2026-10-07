using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_COMPANION_SCRIPTS $71:$01/$02/$04/$05. State 0 waits for Link to mount and
/// validates the companion save-state byte; state 1 clamps after the live
/// companion's own update. Imported records retain source-stream positions.
/// </summary>
internal sealed partial class CompanionBarrierRoomEntity : Node2D,
    IRoomEntity,
    IFixedRoomEntity,
    IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly CompanionBarrierRecord _record;
    private ICompanionBarrierTarget? _target;
    private readonly OracleSaveData _save;
    private readonly Action<int, string, Vector2> _showText;
    private int _state;
    private int _companionId;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal CompanionBarrierRecord Record => _record;
    // Ordered leading placements can join the native pool without assuming
    // allocations for earlier source objects whose owners are still logical.
    internal bool NativeAllocationEnabled { get; set; }
    public bool UpdatesDuringDialogue => _state == 0;
    public bool UpdatesDuringRoomEntityFreeze => _state == 0;

    internal CompanionBarrierRoomEntity(
        CompanionBarrierRecord record,
        ICompanionBarrierTarget? target,
        OracleSaveData save,
        Action<int, string, Vector2> showText)
    {
        if (record.Id != 0x71 || record.SubId is not (1 or 2 or 4 or 5))
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _target = target;
        _save = save;
        _showText = showText;
        Position = new Vector2(record.X, record.Y);
        Name = $"CompanionBarrier_{record.Order}";
    }

    internal void BindTarget(ICompanionBarrierTarget target)
    {
        // The restriction reads the current w1Companion slot every update.
        // Only var30's dialogue index is retained from initialization.
        _target = target;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (_state == 0)
            InitializeBarrier();
        return ScreenTransitionPresentation.Visible;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        // updateInteractions admits only state0 during scroll preload.
        // interactionCode71 checks wLinkDeathTrigger before every subid.
        if (_state == 0 && player?.IsDying == true)
        {
            Finished = true;
            Visible = false;
            return ScreenTransitionPresentation.Hidden;
        }
        return PrepareForScreenTransition(spawns);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (Finished)
            return;

        // The dispatcher decides eligibility first: initialized barriers
        // remain frozen during text, while pending state0 still reaches this
        // entry gate. IsDying is the existing owner of wLinkDeathTrigger.
        if (frame.Player.IsDying)
        {
            Finished = true;
            Visible = false;
            return;
        }

        if (_state == 0)
        {
            InitializeBarrier();
            return;
        }

        if (_state != 1)
        {
            throw new InvalidOperationException(
                $"Companion barrier entered unsupported state ${_state:x2} " +
                $"from {_record.Source}.");
        }
        if (_target is not { BarrierMounted: true }) return;
        bool horizontal = _record.SubId is 1 or 5;
        bool higher = _record.SubId is 1 or 4;
        int boundary = horizontal ? _record.X : _record.Y;
        int position = Mathf.FloorToInt(horizontal ? _target.BarrierPosition.X : _target.BarrierPosition.Y);
        if (higher ? position > boundary : position <= boundary) return;
        _target.SetBarrierCoordinate(horizontal, higher ? unchecked((byte)(boundary + 1)) : boundary);
        _showText(
            _record.TextId(_companionId),
            _record.Message(_companionId),
            _target.BarrierPosition);
    }

    private void InitializeBarrier()
    {
        if (_save.IsCompleted)
        {
            Finished = true;
            return;
        }
        if (_target is not { BarrierMounted: true })
            return;

        _state = 1;
        _companionId = _target.CompanionId; // Interaction.var30 is captured on mount.
        if ((_save.ReadWramByte(
                _record.StateAddress(_companionId)) & 0x80) != 0)
        {
            Finished = true;
        }
    }
}
