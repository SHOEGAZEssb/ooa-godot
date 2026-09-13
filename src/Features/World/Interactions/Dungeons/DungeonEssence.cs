using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// INTERAC_ESSENCE $7f. The entity owns the original object motion and OAM;
/// the room-event layer owns dialogue, music, flags, and the destination warp.
/// </summary>
internal sealed partial class DungeonEssence : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{

    private readonly DungeonObjectRecord _record;
    private readonly DungeonEssenceDefinition _definition;
    private readonly Action<DungeonEssence, Player> _triggered;
    private readonly bool _collected;
    private readonly EnemyAnimationPlayer _essence;
    private readonly DungeonInteractionVisual _pedestalVisual;
    private readonly DungeonInteractionVisual _glowVisual;
    private readonly OracleRoomData _room;
    private readonly Func<long> _animationTick;
    private Action<DungeonEssence> _createChildren = null!;
    private DungeonEssencePedestal? _pedestal;
    private DungeonEssenceGlow? _glow;
    internal bool Initialized { get; private set; }
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;
    internal int DrawZ => _zFixed >> 8;
    private readonly EnemyAnimationPlayer[] _beads = new EnemyAnimationPlayer[8];
    private IReadOnlyList<BlueEnergyBeadRoomEntity> _energyParts = [];
    private Func<Vector2, IReadOnlyList<BlueEnergyBeadRoomEntity>> _createEnergySwirl = null!;
    private Action _stopEnergySwirl = null!;
    private Vector2 _precisePosition;
    private Player? _heldBy;
    private MotionState _state;
    private int _floatCounter;
    private int _zFixed = -0x1000;
    private int _speedZ;
    private int _delay;
    private bool _swirl;
    private bool _triggerSent;

    private static readonly int[] FloatOffsets =
    {
        0, 0, -1, -1, -1, -2, -2, -2,
        -2, -2, -2, -1, -1, -1, -1, 0
    };

    public Node2D Node => this;
    internal bool ReadyForDialogue => _state == MotionState.Held;
    internal bool SwirlActive => _swirl;
    internal bool Collected => _collected;
    internal bool GlowVisible => _glow is not null && GodotObject.IsInstanceValid(_glow) && _glow.Visible;
    internal int Group => _record.Group;
    internal int Room => _record.Room;
    internal int EssenceIndex => _definition.Index;
    internal string Message => _definition.Message;
    internal Warp ExitWarp => _definition.ExitWarp;
    internal int GlowFrameIndex => _glow?.AnimationFrame ?? 0;
    internal bool EnergyBeadVisible(int index) => _energyParts.Any(bead =>
        bead.Index == index && GodotObject.IsInstanceValid(bead) && !bead.Finished && bead.Visible);
    internal Texture2D EnergyBeadTexture(int index) =>
        _beads[index].CurrentTexture;

    internal DungeonEssence(
        DungeonObjectRecord record,
        DungeonInteractionVisual essence,
        DungeonInteractionVisual pedestal,
        DungeonInteractionVisual glow,
        DungeonInteractionVisual bead,
        OracleRoomData room,
        bool collected,
        Func<long> animationTick,
        OracleRandom random,
        Action<DungeonEssence, Player> triggered,
        DungeonEssenceDefinition definition)
    {
        _record = record;
        _definition = definition;
        _triggered = triggered;
        _collected = collected;
        Name = $"DungeonEssence_{definition.Index}";
        Position = record.Position;
        _precisePosition = Position;
        ZIndex = NpcCharacter.InFrontOfLinkZIndex; // objectSetVisible81.
        Visible = false;
        _essence = Load(essence, 0);
        _pedestalVisual = pedestal; _glowVisual = glow;
        _room = room; _animationTick = animationTick;
        for (int index = 0; index < _beads.Length; index++)
            _beads[index] = Load(bead, index);

    }

    public bool BlocksLink(Vector2 linkCenter) => _pedestal?.BlocksLink(linkCenter) == true;

    internal void BindChildren(Action<DungeonEssence> create) => _createChildren = create;
    internal DungeonEssencePedestal CreatePedestal() =>
        _pedestal = new(_record.Position, _pedestalVisual, _room, _animationTick);
    internal DungeonEssenceGlow CreateGlow() => _glow = new(this, _glowVisual);
    private void InitializeState()
    {
        if (Initialized) return;
        Initialized = true;
        _createChildren(this);
        Visible = !_collected;
        _zFixed = -0x1000;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { InitializeState(); return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (frame.Player.IsDying) return; // interactionCode7f's wLinkDeathTrigger gate.
        if (!Initialized) { InitializeState(); return; }
        if (_collected)
        {
            QueueRedraw();
            return;
        }

        switch (_state)
        {
            case MotionState.Waiting:
                // Native state1 returns before both floating and approach
                // detection on the other three updates.
                if ((frame.Counter & 3) != 0) break;
                _floatCounter = (_floatCounter + 1) & 0x0f;
                _zFixed = (-16 + FloatOffsets[_floatCounter]) << 8;
                if (!_triggerSent && CanTrigger(frame.Player))
                {
                    _triggerSent = true;
                    _state = MotionState.Approaching;
                    _triggered(this, frame.Player);
                }
                break;

            case MotionState.Approaching:
            {
                int angle = OracleObjectMovement.Shared.RelativeAngle(
                    _precisePosition, frame.Player.Position);
                Position = OracleObjectMovement.Shared.ApplySpeed(
                    ref _precisePosition, 0x14, angle);
                if (Player.EnemyCollisionOverlaps(frame.Player.Position,
                    new Rect2(Position - new Vector2(4, 4), new Vector2(8, 8))))
                {
                    _state = MotionState.Falling;
                    _speedZ = 0;
                }
                break;
            }

            case MotionState.Falling:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x08) ||
                    (Player.EnemyCollisionOverlaps(frame.Player.Position,
                        new Rect2(Position - new Vector2(6, 4), new Vector2(12, 8))) &&
                     RoomEntityManager.ObjectCollisionZOverlaps(_zFixed >> 8, frame.Player.EnemyContactZ, 7)))
                {
                    _delay = 30;
                    _state = MotionState.Delay;
                }
                break;

            case MotionState.Delay:
                if (--_delay == 0)
                {
                    _heldBy = frame.Player;
                    frame.Player.BeginGetItemTwoHandPose();
                    // State4 writes only yh/xh; the moving essence keeps
                    // its own fractional bytes rather than copying Link's.
                    _precisePosition = frame.Player.Position.Floor() + new Vector2(0, -14) +
                        _precisePosition - _precisePosition.Floor();
                    Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                    _zFixed = 0;
                    _state = MotionState.Held;
                }
                break;

        }

        QueueRedraw();
    }

    internal void BindEnergySwirl(Func<Vector2, IReadOnlyList<BlueEnergyBeadRoomEntity>> create, Action stop)
    { _createEnergySwirl = create; _stopEnergySwirl = stop; }

    internal void StartEnergySwirl()
    {
        _swirl = true;
        _energyParts = _createEnergySwirl(Position);
        QueueRedraw();
    }

    internal void StopEnergySwirl()
    {
        if (_swirl) _stopEnergySwirl();
        _swirl = false;
        QueueRedraw();
    }

    internal void ReleasePlayerPose()
    {
        _heldBy?.EndGetItemTwoHandPose();
        _heldBy = null;
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        Vector2 transition = TransitionDrawOffset;
        if (_collected || !Initialized)
            return;

        int z = _zFixed >> 8;
        Vector2 itemOffset = new Vector2(-16, -16 + z) + transition;
        DrawTexture(_essence.CurrentTexture, itemOffset);

    }

    private EnemyAnimationPlayer Load(
        DungeonInteractionVisual visual,
        int animation)
    {
        var player = new EnemyAnimationPlayer(this, visual.Animations.Length);
        player.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        player.SetAnimation(animation);
        return player;
    }

    private bool CanTrigger(Player player)
    {
        if (!player.IsGroundedForFloorButton || player.IsCarryingObject ||
            player.IsHoldingItemOneHand || player.IsHoldingItemTwoHands)
        {
            return false;
        }
        Vector2 delta = player.Position.Floor() - Position.Floor();
        // objectCheckCenteredWithLink includes +/-4. The distance helper
        // uses Manhattan distance, with ties selecting the horizontal axis.
        float x = Mathf.Abs(delta.X);
        return delta.Y > x && x <= 4 && delta.Y + x < 20;
    }

}

internal enum MotionState
{
    Waiting,
    Approaching,
    Falling,
    Delay,
    Held
}

internal readonly record struct DungeonEssenceDefinition(
    int Index,
    string Message,
    Warp ExitWarp);
