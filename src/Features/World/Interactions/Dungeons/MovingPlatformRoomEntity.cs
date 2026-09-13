using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MOVING_PLATFORM $79, dungeon-keyed native mini-script.</summary>
internal sealed partial class MovingPlatformRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly MovingPlatformScript _program;
    private readonly MovingPlatformRidingState _riding;
    private readonly Func<int> _playingInstrument;
    private readonly int _speed;
    private Vector2 _precisePosition;
    private bool _initialized;
    private bool _moving;
    private int _counter;
    private int _command;
    private int _angle;

    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int Script { get; }
    internal bool LinkRiding => _riding.IsOwner(this);
    bool IPlayerRideableRoomEntity.LinkRiding => LinkRiding;
    internal Vector2 CollisionRadii { get; }
    internal Vector2 PrecisePosition => _precisePosition;
    internal int Counter => _counter;
    internal int Command => _command;
    internal int Angle => _angle;
    internal bool Moving => _moving;

    internal MovingPlatformRoomEntity(DungeonInteractionVisual visual, Vector2 position, int rawSubId,
        Vector2 collisionRadii, DungeonInteractionDatabase data, MovingPlatformScript program,
        MovingPlatformRidingState riding, Func<int> playingInstrument)
    {
        Script = rawSubId >> 3;
        CollisionRadii = collisionRadii;
        _speed = data.Constant("platform-speed");
        _program = program;
        _riding = riding;
        _playingInstrument = playingInstrument;
        _precisePosition = position;
        Name = $"MovingPlatform_{Script}";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        InitializeVisual(visual, position, positionedOam: true);
        Visible = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            RunScript();
            Visible = true;
        }
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            PrepareForScreenTransition(spawns);
            return;
        }
        Vector2 link = OracleObjectMath.ToPixelPosition(frame.Player.Position) + Vector2.Down * 5;
        _riding.CheckContact(this, Math.Abs(link.X - Position.X) < CollisionRadii.X &&
            Math.Abs(link.Y - Position.Y) < CollisionRadii.Y);
        if (_moving)
        {
            // Instrument freezes moving substate 1, including its counter.
            // Substate 0 still decrements its wait and may select a move.
            if (_playingInstrument() != 0) return;
            Position = OracleObjectMovement.Shared.ApplySpeed(ref _precisePosition, _speed, _angle);
            if (LinkRiding && frame.Player.CanBeCarriedByMovingPlatform)
                frame.Player.AdvanceInteractionVelocity(_speed, _angle);
        }
        _counter = (_counter - 1) & 0xff;
        if (_counter == 0) RunScript();
        QueueRedraw();
    }

    private void RunScript()
    {
        for (int dispatched = 0; dispatched < _program.Commands.Count; dispatched++)
        {
            if (_command >= _program.Commands.Count)
                throw new InvalidOperationException($"Moving platform script escaped {_program.Source}.");
            var command = _program.Commands[_command++];
            if (command.Opcode == 4) { _command = command.Operand; continue; }
            _counter = command.Operand;
            _moving = command.Opcode != 0;
            if (_moving) _angle = (command.Opcode - 8) * 8;
            return;
        }
        throw new InvalidOperationException($"Non-yielding moving-platform loop at {_program.Source}.");
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
