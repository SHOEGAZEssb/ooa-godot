using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_1 $6b:$0d in room 5:bf.</summary>
internal sealed partial class Room5bfSlidingBlock : NpcCharacter,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker
{
    private readonly Room5bfLeverState _leverState;
    private readonly Room5bfConstants _constants;
    private readonly int _baseX;
    private readonly bool _movesRight;

    public Node2D Node => this;
    internal int PullOffset { get; private set; }

    internal Room5bfSlidingBlock(
        Room5bfInteractionRecord record,
        Room5bfLeverState leverState,
        Room5bfConstants constants,
        Color[] palette)
    {
        if (record.Kind != Room5bfInteractionKind.SlidingBlock ||
            record.Var03 is not (0 or 1))
        {
            throw new ArgumentException(
                $"Room 5:bf sliding block has invalid source record " +
                $"${record.Id:x2}:${record.SubId:x2}.", nameof(record));
        }

        _leverState = leverState;
        _constants = constants;
        _baseX = record.X;
        _movesRight = record.Var03 != 0;
        Name = $"Room5bfSlidingBlock_{record.Order}";
        ZIndex = BehindLinkZIndex;
        Initialize(record.ToNpcRecord());
        SetCollisionRadii(constants.BlockRadius, constants.BlockRadius);
        SetScriptPaletteOverride(palette);
        SetScriptAnimation(record.Animations[0]);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        int pullDistance = _leverState.PullDistance;
        PullOffset = (pullDistance & _constants.DistanceMask) >>
            _constants.DistanceShift;
        int x = _movesRight
            ? _baseX + PullOffset
            : _baseX - PullOffset;

        if (!_movesRight && PullOffset is 1 or 2 &&
            !frame.Player.SideScrollAirborne &&
            Math.Abs(Mathf.FloorToInt(frame.Player.Position.Y) -
                _constants.SquishY) <= _constants.SquishRange &&
            Math.Abs(Mathf.FloorToInt(frame.Player.Position.X) -
                _constants.SquishX) <= _constants.SquishRange)
        {
            frame.Player.ForceSideScrollSquish();
        }

        SetStatePosition(new Vector2(x, Position.Y));
        AnimateAsNpcOneUpdate(frame.Player);
    }

    public bool BlocksLink(Vector2 linkCenter) => BlocksLinkCenter(linkCenter);

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);
}
