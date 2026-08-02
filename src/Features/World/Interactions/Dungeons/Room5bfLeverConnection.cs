using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>The source-created $61:$80 graphical lever connection.</summary>
internal sealed partial class Room5bfLeverConnection : NpcCharacter,
    IRoomEntity, IFixedRoomEntity
{
    private static readonly int[] DownwardYOffsets = [0, 8, 16, 24, 32];

    private readonly Room5bfInteractionRecord _record;
    private readonly Room5bfLever _lever;
    private readonly Room5bfConstants _constants;
    private int _phase;

    public Node2D Node => this;
    internal int Phase => _phase;

    internal Room5bfLeverConnection(
        Room5bfInteractionRecord record,
        Room5bfLever lever,
        Room5bfConstants constants)
    {
        if (record.Kind != Room5bfInteractionKind.LeverConnection ||
            record.Animations.Count != DownwardYOffsets.Length)
        {
            throw new ArgumentException(
                "Room 5:bf lever connection has invalid source data.",
                nameof(record));
        }

        _record = record;
        _lever = lever;
        _constants = constants;
        Name = "Room5bfLeverConnection";
        ZIndex = FixedLowPriorityZIndex;
        Initialize(record.ToNpcRecord());
        SetBlocksLink(false);
        SetFixedDrawPriority(FixedLowPriorityZIndex);
        SetScriptAnimation(record.Animations[0]);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        int distance = Math.Abs(
            Mathf.FloorToInt(_lever.Position.Y) - _lever.BaseY);
        int phase = Math.Clamp(
            distance / _constants.ConnectionStep,
            0,
            DownwardYOffsets.Length - 1);
        SetStatePosition(new Vector2(
            _lever.Position.X,
            _lever.BaseY + DownwardYOffsets[phase]));
        if (phase == _phase)
            return;

        _phase = phase;
        SetScriptAnimation(_record.Animations[_phase]);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);
}
