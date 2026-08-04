using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_COMPANION_SCRIPTS $71:$02. State 0 waits for Link to mount and
/// validates the companion save-state byte; state 1 clamps after the live
/// companion's own update, at this placed object's source-stream position.
/// </summary>
internal sealed partial class CompanionBarrierRoomEntity : Node2D,
    IRoomEntity,
    IFixedRoomEntity,
    IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private readonly CompanionBarrierRecord _record;
    private ICompanionBarrierTarget? _target;
    private readonly OracleSaveData _save;
    private readonly Action<int, string, Vector2> _showText;
    private int _state;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal CompanionBarrierRecord Record => _record;
    internal ICompanionBarrierTarget? Target => _target;

    internal CompanionBarrierRoomEntity(
        CompanionBarrierRecord record,
        ICompanionBarrierTarget? target,
        OracleSaveData save,
        Action<int, string, Vector2> showText)
    {
        if (record is not { Id: 0x71, SubId: 0x02 })
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
        _target ??= target;
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

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        if (Finished)
            return;

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
        if (_target is not { BarrierMounted: true } ||
            Mathf.FloorToInt(_target.BarrierPosition.Y) <= _record.Y)
        {
            return;
        }

        _target.ClampToLowerY(_record.Y);
        _showText(
            _record.TextId(_target.CompanionId),
            _record.Message(_target.CompanionId),
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
        if ((_save.ReadWramByte(
                _record.StateAddress(_target.CompanionId)) & 0x80) != 0)
        {
            Finished = true;
        }
    }
}
