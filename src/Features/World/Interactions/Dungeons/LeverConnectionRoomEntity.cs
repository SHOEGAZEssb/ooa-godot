using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>The source-created $61:$80 graphical lever connection.</summary>
internal sealed partial class LeverConnectionRoomEntity : NpcCharacter,
    IRoomEntity, IFixedRoomEntity
{
    private static readonly int[] DownwardYOffsets = [0, 8, 16, 24, 32];

    private readonly IReadOnlyList<string> _animations;
    private readonly LeverRoomEntity _lever;
    private readonly int _connectionStep;
    private int _phase;

    public Node2D Node => this;
    internal int Phase => _phase;

    internal LeverConnectionRoomEntity(
        NpcRecord record, IReadOnlyList<string> animations,
        LeverRoomEntity lever, int connectionStep)
    {
        if (animations.Count != DownwardYOffsets.Length)
        {
            throw new ArgumentException(
                "Lever connection has invalid source data.",
                nameof(record));
        }

        _animations = animations;
        _lever = lever;
        _connectionStep = connectionStep;
        Name = "LeverConnection";
        ZIndex = FixedLowPriorityZIndex;
        Initialize(record);
        SetBlocksLink(false);
        SetFixedDrawPriority(FixedLowPriorityZIndex);
        SetScriptAnimation(animations[0]);
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
            distance / _connectionStep,
            0,
            DownwardYOffsets.Length - 1);
        SetStatePosition(new Vector2(
            _lever.Position.X,
            _lever.BaseY + DownwardYOffsets[phase] * _lever.DirectionSign));
        if (phase == _phase)
            return;

        _phase = phase;
        SetScriptAnimation(_animations[_phase]);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);
}
