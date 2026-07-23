using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_COLORED_CUBE $19:$05.</summary>
internal sealed partial class SpiritsGraveColoredCube : SpiritsGraveVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private static readonly int[,] RollAnimations =
    {
        { 0x12, 0x07, 0x13, 0x06 },
        { 0x14, 0x11, 0x15, 0x10 },
        { 0x16, 0x0b, 0x17, 0x0a },
        { 0x18, 0x09, 0x19, 0x08 },
        { 0x1a, 0x0f, 0x1b, 0x0e },
        { 0x1c, 0x0d, 0x1d, 0x0c }
    };
    private static readonly int[] Colors = { 1, 0, 0, 2, 2, 1 };

    private readonly OracleRoomData _room;
    private readonly SpiritsGravePuzzleState _puzzle;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private int _orientation;
    private int _pushCounter = 20;
    private int _holeCounter = 10;
    private bool _moving;
    private int _lastFrame = -1;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Orientation => _orientation;
    internal int PushCounter => _pushCounter;
    internal int HoleCounter => _holeCounter;
    internal bool Moving => _moving;

    internal SpiritsGraveColoredCube(
        ObjectRecord record,
        VisualRecord visual,
        OracleRoomData room,
        SpiritsGravePuzzleState puzzle,
        IReadOnlyDictionary<int, Color[]> cubePalettes,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _room = room;
        _puzzle = puzzle;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _orientation = record.SubId;
        Name = "SpiritsGraveColoredCube";
        ZIndex = 9;
        InitializeVisual(
            visual, record.Position, _orientation,
            paletteOverrides: cubePalettes);
        SetCubeCollision(0x0f);
        UpdatePuzzleState();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_moving)
        {
            AdvanceAnimation();
            if (AnimationFrame != _lastFrame)
            {
                _lastFrame = AnimationFrame;
                ApplyAnimationParameter(AnimationParameter);
            }
            QueueRedraw();
            return;
        }

        if (_room.GetMetatile(Position) == 0x4d)
        {
            if (--_holeCounter == 0)
            {
                _room.SetPositionTileAndCollision(Position, 0xf3, null, _animationTick());
                _roomTileChanged();
                spawns.Add(new FallingDownHoleSpawn(Position));
                _puzzle.CubePosition = 0;
                Finished = true;
            }
        }
        else
        {
            _holeCounter = 10;
        }

        if (!TryGetPushDirection(frame.Player, out Vector2I direction) ||
            !DestinationIsOpen(direction))
        {
            _pushCounter = 20;
            return;
        }
        if (--_pushCounter != 0)
            return;

        int directionIndex = direction == Vector2I.Up ? 0
            : direction == Vector2I.Right ? 1
            : direction == Vector2I.Down ? 2
            : 3;
        // interactionCode19 clears wRoomCollisions at the old cell for the
        // duration of the roll, then reinstalls $0f at the centered endpoint.
        SetCubeCollision(0x00);
        _moving = true;
        _lastFrame = -1;
        SetAnimation(RollAnimations[_orientation, directionIndex]);
        ApplyAnimationParameter(AnimationParameter);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private bool TryGetPushDirection(Player player, out Vector2I direction)
    {
        direction = player.FacingVector;
        if (!player.IsAttemptingObjectPush(direction) || direction == Vector2I.Zero)
            return false;
        Vector2 delta = Position - player.Position;
        Vector2 expected = (Vector2)direction;
        float forward = delta.Dot(expected);
        float perpendicular = Math.Abs(delta.Dot(new Vector2(-expected.Y, expected.X)));
        return forward is >= 10 and < 22 && perpendicular < 7;
    }

    private bool DestinationIsOpen(Vector2I direction)
    {
        Vector2 target = Position + (Vector2)direction * 16.0f;
        TerrainInfo terrain = _room.GetTerrainInfo(target);
        return terrain.Tile != 0xff && (terrain.Collision & 0x0f) == 0;
    }

    private void ApplyAnimationParameter(int parameter)
    {
        if ((parameter & 0x80) != 0)
        {
            _orientation = parameter & 0x7f;
            _moving = false;
            _pushCounter = 20;
            _holeCounter = 10;
            Position = new Vector2(
                Mathf.Floor(Position.X / 16.0f) * 16.0f + 8.0f,
                Mathf.Floor(Position.Y / 16.0f) * 16.0f + 8.0f);
            SetAnimation(_orientation);
            SetCubeCollision(0x0f);
            UpdatePuzzleState();
            _playSound(0x7f);
            return;
        }
        Vector2 offset = parameter switch
        {
            2 => Vector2.Up * 4.0f,
            4 => Vector2.Right * 4.0f,
            6 => Vector2.Down * 4.0f,
            8 => Vector2.Left * 4.0f,
            _ => Vector2.Zero
        };
        Position += offset;
    }

    private void UpdatePuzzleState()
    {
        _puzzle.CubePosition = _room.GetPackedPosition(Position);
        _puzzle.CubeColor = Colors[_orientation];
    }

    private void SetCubeCollision(byte collision) =>
        _room.SetPositionTileAndCollision(
            Position,
            _room.GetMetatile(Position),
            collision,
            _animationTick(),
            preserveRenderedTile: true);
}
