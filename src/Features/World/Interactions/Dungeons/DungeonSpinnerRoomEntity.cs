using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_SPINNER $7d and its related arrow interaction. Link's circular
/// positions are driven by animation parameters exactly once per source
/// frame; the final $ff parameter hands off to LINK_STATE_FORCE_MOVEMENT.
/// </summary>
internal sealed partial class DungeonSpinnerRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerRestriction, IPlayerForcedMovement
{
    private const int InitialWait = 30;
    private const int ExitUpdates = 0x10;
    private const int CollisionRadius = 9 + 6;

    private static readonly Vector2[] LinkRelativePositions =
    [
        new(0x00, 0x0c),
        new(0x02, 0x0a),
        new(0x08, 0x08),
        new(0x0a, 0x02),
        new(0x0c, 0x00),
        new(0x0a, -0x02),
        new(0x08, -0x08),
        new(0x02, -0x0a),
        new(0x00, -0x0c),
        new(-0x02, -0x0a),
        new(-0x08, -0x08),
        new(-0x0a, -0x02),
        new(-0x0c, 0x00),
        new(-0x0a, 0x02),
        new(-0x08, 0x08),
        new(-0x02, 0x0a)
    ];

    private static readonly Vector2I[] DirectionVectors =
    [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];

    private readonly DungeonSpinnerPlacement _placement;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action<int> _beginScreenShake;
    private readonly EnemyAnimationPlayer _spinnerAnimation;
    private readonly EnemyAnimationPlayer _arrowAnimation;
    private SpinnerPhase _phase = SpinnerPhase.Waiting;
    private bool _initializing = true;
    private bool _waitNeedsStart = true;
    private bool _red;
    private int _waitCounter;
    private int _exitCounter;
    private int _exitDirection;
    private int _positionBase;
    private int _turnFrameSeen;
    private Vector2 _linkOffset;

    public Node2D Node => this;
    public bool DisablesSword => _phase != SpinnerPhase.Waiting;
    public bool DisablesItems => _phase != SpinnerPhase.Waiting;
    public bool DisablesMovement => _phase != SpinnerPhase.Waiting;
    public bool DisablesMenus => _phase != SpinnerPhase.Waiting;
    public bool DisablesScreenTransitions => _phase != SpinnerPhase.Waiting;

    internal SpinnerPhase Phase => _phase;
    internal bool Red => _red;
    internal int WaitCounter => _waitCounter;
    internal int ExitCounter => _exitCounter;
    internal int ExitDirection => _exitDirection;
    internal int SpinnerAnimationIndex => _spinnerAnimation.AnimationIndex;
    internal int SpinnerAnimationFrame => _spinnerAnimation.FrameIndex;
    internal int ArrowAnimationIndex => _arrowAnimation.AnimationIndex;
    internal Vector2 LinkOffset => _linkOffset;
    internal Texture2D SpinnerTexture =>
        _spinnerAnimation.CurrentTextureForPalette(_red ? 5 : 4);
    internal Texture2D ArrowTexture =>
        _arrowAnimation.CurrentTextureForPalette(_red ? 5 : 4);

    internal DungeonSpinnerRoomEntity(
        DungeonSpinnerPlacement placement,
        OracleRuntimeState runtime,
        DungeonInteractionVisual visual,
        Action<int> playSound,
        Action<int> beginScreenShake)
    {
        _placement = placement;
        _runtime = runtime;
        _playSound = playSound;
        _beginScreenShake = beginScreenShake;
        Position = new Vector2(
            (placement.PackedPosition & 0x0f) * 16 + 8,
            (placement.PackedPosition >> 4) * 16 + 8);
        Name = $"Spinner_{placement.Group}_{placement.Room:x2}_" +
            $"{placement.PackedPosition:x2}";
        ZIndex = NpcCharacter.BehindLinkZIndex;

        _red = (_runtime.ReadWramByte(OracleRuntimeState.SpinnerStateAddress) &
            placement.StateMask) != 0;
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites);
        _spinnerAnimation = CreateAnimation(source, visual);
        _arrowAnimation = CreateAnimation(source, visual);
        _spinnerAnimation.SetAnimation(_red ? 1 : 0);
        _arrowAnimation.SetAnimation(_red ? 3 : 2);
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_phase is SpinnerPhase.Touched or SpinnerPhase.Turning)
        {
            player.SetSpinnerTurnPosition(Position + _linkOffset, _exitDirection);
        }
        else if (_phase == SpinnerPhase.Exiting)
        {
            player.AdvanceSpinnerExit(DirectionVectors[_exitDirection]);
        }
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_initializing)
        {
            _initializing = false;
        }
        else
        {
            switch (_phase)
            {
                case SpinnerPhase.Waiting:
                    UpdateWaiting(frame.Player);
                    break;
                case SpinnerPhase.Touched:
                    UpdateTouched(frame.Player);
                    break;
                case SpinnerPhase.Turning:
                    UpdateTurning(frame.Player);
                    break;
                case SpinnerPhase.Exiting:
                    UpdateExiting(frame.Player);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported spinner phase {_phase} in {_placement.Source}.");
            }
        }

        _arrowAnimation.Advance();
        QueueRedraw();
    }

    private void UpdateWaiting(Player player)
    {
        if (_waitNeedsStart)
        {
            _waitCounter = InitialWait;
            _waitNeedsStart = false;
            return;
        }
        if (_waitCounter != 0 && --_waitCounter != 0)
            return;
        if (player.TopDownAirborne || player.IsDying || !TouchesLink(player))
            return;

        _linkOffset = player.PrecisePosition - Position;
        _exitDirection = DirectionFromSpinner(player.PrecisePosition);
        _spinnerAnimation.SetAnimation(_red ? 1 : 0);
        _turnFrameSeen = 0;
        _phase = SpinnerPhase.Touched;
        player.BeginSpinnerTouch();
    }

    private void UpdateTouched(Player player)
    {
        if (player.TopDownAirborne || player.TopDownSwimming)
        {
            _phase = SpinnerPhase.Waiting;
            _waitCounter = 0;
            player.EndSpinnerControl();
            return;
        }

        int entryDirection = DirectionFromSpinner(player.PrecisePosition);
        (_linkOffset, _exitDirection, _positionBase) =
            TurnData(entryDirection, clockwise: _red);
        _phase = SpinnerPhase.Turning;
        player.BeginSpinnerTurn(Position + _linkOffset, _exitDirection);
        _beginScreenShake(4);
        _playSound(OracleSoundEngine.SndOpenChest);
    }

    private void UpdateTurning(Player player)
    {
        int parameter = _spinnerAnimation.CurrentParameter;
        if (parameter == 0xff)
        {
            _phase = SpinnerPhase.Exiting;
            _exitCounter = ExitUpdates;
            player.BeginSpinnerExit(_exitDirection);
            return;
        }

        int frame = _spinnerAnimation.FrameIndex;
        if (frame != _turnFrameSeen)
        {
            _turnFrameSeen = frame;
            if (parameter != 0)
            {
                _linkOffset = LinkRelativePositions[
                    (_positionBase + parameter) & 0x0f];
                player.SetSpinnerTurnPosition(
                    Position + _linkOffset, _exitDirection);
                _playSound(OracleSoundEngine.SndDoorClose);
            }
        }
        _spinnerAnimation.Advance();
    }

    private void UpdateExiting(Player player)
    {
        if (--_exitCounter != 0)
            return;

        byte state = _runtime.ReadWramByte(
            OracleRuntimeState.SpinnerStateAddress);
        _runtime.SetWramByte(
            OracleRuntimeState.SpinnerStateAddress,
            (byte)(state ^ _placement.StateMask));
        _red = !_red;
        _arrowAnimation.SetAnimation(_red ? 3 : 2);
        _phase = SpinnerPhase.Waiting;
        _waitNeedsStart = true;
        player.EndSpinnerControl();
    }

    private bool TouchesLink(Player player)
    {
        Vector2 delta = player.PrecisePosition - Position;
        return Math.Abs(delta.X) < CollisionRadius &&
            Math.Abs(delta.Y) < CollisionRadius;
    }

    private int DirectionFromSpinner(Vector2 point)
    {
        Vector2 delta = point - Position;
        if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
            return delta.X >= 0 ? 1 : 3;
        return delta.Y >= 0 ? 2 : 0;
    }

    private static (Vector2 Offset, int ExitDirection, int PositionBase)
        TurnData(int entryDirection, bool clockwise) =>
        (entryDirection, clockwise) switch
        {
            (0, false) => (new Vector2(0, -12), 3, 8),
            (1, false) => (new Vector2(12, 0), 0, 4),
            (2, false) => (new Vector2(0, 12), 1, 0),
            (3, false) => (new Vector2(-12, 0), 2, 12),
            (0, true) => (new Vector2(0, -12), 1, 8),
            (1, true) => (new Vector2(12, 0), 2, 4),
            (2, true) => (new Vector2(0, 12), 3, 0),
            (3, true) => (new Vector2(-12, 0), 0, 12),
            _ => throw new ArgumentOutOfRangeException(nameof(entryDirection))
        };

    private EnemyAnimationPlayer CreateAnimation(
        Image source,
        DungeonInteractionVisual visual)
    {
        var animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        animation.Load(
            source,
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted,
            positionedOam: true,
            paletteVariants: [4, 5]);
        return animation;
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (!Visible)
            return;
        int palette = _red ? 5 : 4;
        // The parent occupies the lower interaction slot and therefore wins
        // OAM overlap. Draw its later-created arrow first to preserve that.
        DrawTexture(
            _arrowAnimation.CurrentTextureForPalette(palette),
            _arrowAnimation.CurrentOffset + SourceOamDrawOffset);
        DrawTexture(
            _spinnerAnimation.CurrentTextureForPalette(palette),
            _spinnerAnimation.CurrentOffset + SourceOamDrawOffset);
    }
}

internal enum SpinnerPhase
{
    Waiting,
    Touched,
    Turning,
    Exiting
}
