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
    IRoomEntity, IFixedRoomEntity, IPlayerRestriction, IPlayerForcedMovement,
    IRoomEntityUpdateFreeze, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
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

    private readonly DungeonSpinnerPlacement _placement;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action<int> _beginScreenShake;
    private readonly EnemyAnimationPlayer _spinnerAnimation;
    private readonly DungeonInteractionVisual _visual;
    private Func<DungeonSpinnerRoomEntity,DungeonInteractionVisual,DungeonSpinnerArrowRoomEntity?>? _createArrow;
    private SpinnerPhase _phase = SpinnerPhase.Waiting;
    private bool _initializing = true;
    private bool _collisionRadiiPending = true;
    private bool _touchScriptPending;
    private bool _waitNeedsStart = true;
    private bool _red;
    private int _waitCounter;
    private int _exitCounter;
    private int _exitDirection;
    private int _positionBase;
    private Vector2 _linkOffset;

    public Node2D Node => this;
    public bool DisablesSword => _phase is SpinnerPhase.Turning or SpinnerPhase.Exiting;
    public bool DisablesItems => _phase is SpinnerPhase.Turning or SpinnerPhase.Exiting;
    public bool DisablesMovement => _phase != SpinnerPhase.Waiting;
    public bool DisablesMenus => _phase != SpinnerPhase.Waiting;
    public bool DisablesScreenTransitions => _phase != SpinnerPhase.Waiting;
    public bool DisablesCompanion => _phase == SpinnerPhase.Turning;
    public bool FreezesRoomEntities => _phase == SpinnerPhase.Turning;
    public bool FreezesInteractions => false;
    public bool UpdatesDuringDialogue => _initializing;
    public bool UpdatesDuringRoomEntityFreeze => _initializing;

    internal SpinnerPhase Phase => _phase;
    internal bool Red => _red;
    internal int WaitCounter => _waitCounter;
    internal int ExitCounter => _exitCounter;
    internal int ExitDirection => _exitDirection;
    internal int SpinnerAnimationIndex => _spinnerAnimation.AnimationIndex;
    internal int SpinnerAnimationFrame => _spinnerAnimation.FrameIndex;
    internal DungeonSpinnerArrowRoomEntity? Arrow { get; private set; }
    internal Vector2 LinkOffset => _linkOffset;
    internal Texture2D SpinnerTexture =>
        _spinnerAnimation.CurrentTextureForPalette(_red ? 5 : 4);

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
        _visual = visual;
        Position = new Vector2(
            (placement.PackedPosition & 0x0f) * 16 + 8,
            (placement.PackedPosition >> 4) * 16 + 8);
        Name = $"Spinner_{placement.Group}_{placement.Room:x2}_" +
            $"{placement.PackedPosition:x2}";
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        Visible = false;

        _red = (_runtime.ReadWramByte(OracleRuntimeState.SpinnerStateAddress) &
            placement.StateMask) != 0;
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites);
        _spinnerAnimation = CreateAnimation(source, visual);
        _spinnerAnimation.SetAnimation(_red ? 1 : 0);
    }

    internal void BindArrow(Func<DungeonSpinnerRoomEntity,DungeonInteractionVisual,DungeonSpinnerArrowRoomEntity?> createArrow)
        => _createArrow = createArrow;

    private void Initialize()
    {
        if (!_initializing) return;
        _initializing = false;
        _red = (_runtime.ReadWramByte(OracleRuntimeState.SpinnerStateAddress)&_placement.StateMask) != 0;
        _spinnerAnimation.SetAnimation(_red ? 1 : 0);
        Visible = true;
        Arrow = (_createArrow ?? throw new InvalidOperationException($"INTERAC$7d missing checked arrow allocator at {_placement.Source}."))(this,_visual);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_phase == SpinnerPhase.Turning)
        {
            player.SetSpinnerTurnPosition(Position + _linkOffset, _exitDirection);
        }
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_initializing)
        {
            Initialize();
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

        QueueRedraw();
    }

    private void UpdateWaiting(Player player)
    {
        // interactionRunScript checks the death trigger before either counter.
        if (player.IsDying) return;
        // spinnerScript_initialization's setcollisionradii clears carry.
        // The following wait30 is installed on the next script update.
        if (_collisionRadiiPending)
        {
            _collisionRadiiPending = false;
            return;
        }
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
        _phase = SpinnerPhase.Touched;
        _touchScriptPending = true;
        player.BeginSpinnerTouch();
    }

    private void UpdateTouched(Player player)
    {
        // setanimationfromangle yields while state1 retains wcc95 bit7;
        // the next script update only executes incstate, before state2 runs.
        if (_touchScriptPending)
        {
            if (player.IsDying) return;
            _touchScriptPending = false;
            return;
        }
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
        _playSound(SoundId.SndOpenChest);
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

        if (parameter != 0)
        {
            _spinnerAnimation.ConsumeParameter();
            _linkOffset = LinkRelativePositions[(_positionBase + parameter) & 0x0f];
            player.SetSpinnerTurnPosition(Position + _linkOffset, _exitDirection);
            _playSound(SoundId.SndDoorClose);
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
        _phase = SpinnerPhase.Waiting;
        _waitNeedsStart = true;
        player.EndSpinnerControl();
    }

    private bool TouchesLink(Player player)
    {
        Vector2 point = OracleObjectMath.ToPixelPosition(player.PrecisePosition);
        int x = unchecked((byte)((int)point.X-(int)Position.X+CollisionRadius));
        int y = unchecked((byte)((int)point.Y-(int)Position.Y+CollisionRadius));
        return x < CollisionRadius*2 && y < CollisionRadius*2;
    }

    private int DirectionFromSpinner(Vector2 point)
    {
        // objectCheckLinkWithinDistance compares yh/xh, including horizontal
        // priority for equal pixel distances. Fractional bytes do not decide.
        Vector2 delta = OracleObjectMath.ToPixelPosition(point) - Position;
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
