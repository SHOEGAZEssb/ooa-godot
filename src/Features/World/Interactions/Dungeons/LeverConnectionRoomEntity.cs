using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>The source-created $61:$80 graphical lever connection.</summary>
internal sealed partial class LeverConnectionRoomEntity : NpcCharacter,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private static readonly int[] DownwardYOffsets = [0, 8, 16, 24, 32];

    private readonly IReadOnlyList<string> _animations;
    private readonly LeverRoomEntity _lever;
    private readonly int _connectionStep;
    private int _phase;
    private bool _initialized;

    public Node2D Node => this;
    internal int Phase => _phase;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

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
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        Initialize(record);
        SetBlocksLink(false);
        SetScriptAnimation(animations[0]);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        Advance();
    }

    private void Advance()
    {
        // The child's state0 also calls objectSetVisible83, after graphics
        // initialization. Preserve the cleared visibility gate until then.
        if (!_initialized) SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
        _initialized = true;
        int distance = Math.Abs(
            Mathf.FloorToInt(_lever.Position.Y) - _lever.BaseY);
        int phase = Math.Clamp(
            distance / _connectionStep,
            0,
            DownwardYOffsets.Length - 1);
        SetStatePosition(new Vector2(
            _lever.Position.X,
            _lever.BaseY + DownwardYOffsets[phase] * _lever.DirectionSign));
        _phase = phase;
        SetScriptAnimation(_animations[_phase]);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (!_initialized) Advance(); return ScreenTransitionPresentation.Visible; }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);
}
