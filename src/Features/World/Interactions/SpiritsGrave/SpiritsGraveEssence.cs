using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_ESSENCE $7f. The entity owns the original object motion and OAM;
/// the room-event layer owns dialogue, music, flags, and the destination warp.
/// </summary>
internal sealed partial class SpiritsGraveEssence : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker
{

    private readonly ObjectRecord _record;
    private readonly Action<SpiritsGraveEssence, Player> _triggered;
    private readonly OracleRandom _random;
    private readonly bool _collected;
    private readonly EnemyAnimationPlayer _essence;
    private readonly EnemyAnimationPlayer _pedestal;
    private readonly EnemyAnimationPlayer _glow;
    private readonly EnemyAnimationPlayer[] _beads = new EnemyAnimationPlayer[8];
    private readonly int[] _beadDelay = new int[8];
    private readonly Vector2[] _beadPosition = new Vector2[8];
    private readonly bool[] _beadVisible = new bool[8];
    private Vector2 _precisePosition;
    private Player? _heldBy;
    private MotionState _state;
    private int _floatCounter;
    private int _zFixed = -0x1000;
    private int _speedZ;
    private int _delay;
    private bool _swirl;
    private bool _beadsInitialized;
    private bool _triggerSent;

    private static readonly int[] FloatOffsets =
    {
        0, 0, -1, -1, -1, -2, -2, -2,
        -2, -2, -2, -1, -1, -1, -1, 0
    };

    public Node2D Node => this;
    internal bool ReadyForDialogue => _state == MotionState.Held;
    internal bool SwirlActive => _swirl;
    internal int Motion => (int)_state;
    internal int Delay => _delay;
    internal bool Collected => _collected;

    internal SpiritsGraveEssence(
        ObjectRecord record,
        VisualRecord essence,
        VisualRecord pedestal,
        VisualRecord glow,
        VisualRecord bead,
        OracleRoomData room,
        bool collected,
        Func<long> animationTick,
        OracleRandom random,
        Action<SpiritsGraveEssence, Player> triggered)
    {
        _record = record;
        _random = random;
        _triggered = triggered;
        _collected = collected;
        Name = "SpiritsGraveEternalSpirit";
        Position = record.Position;
        _precisePosition = Position;
        ZIndex = 9;
        _essence = Load(essence, 0);
        _pedestal = Load(pedestal, 0);
        _glow = Load(glow, 0);
        for (int index = 0; index < _beads.Length; index++)
            _beads[index] = Load(bead, index);

        // interaction7f_subid01 writes $0f to the collision byte at the
        // pedestal's packed position. The pedestal is created before the
        // essence checks ROOMFLAG_ITEM, so this remains true after collection.
        room.SetPositionTileAndCollision(
            record.Position,
            room.GetMetatile(record.Position),
            0x0f,
            animationTick(),
            preserveRenderedTile: true);
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        Vector2 delta = linkCenter - _record.Position;
        return Mathf.Abs(delta.X) < 10 && Mathf.Abs(delta.Y) < 6;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _pedestal.Advance();
        if (_collected)
        {
            QueueRedraw();
            return;
        }
        _essence.Advance();
        _glow.Advance();

        switch (_state)
        {
            case MotionState.Waiting:
                if ((frame.Counter & 3) == 0)
                {
                    _floatCounter = (_floatCounter + 1) & 0x0f;
                    _zFixed = (-16 + FloatOffsets[_floatCounter]) << 8;
                }
                if (!_triggerSent && CanTrigger(frame.Player))
                {
                    _triggerSent = true;
                    _state = MotionState.Approaching;
                    _triggered(this, frame.Player);
                }
                break;

            case MotionState.Approaching:
            {
                int angle = OracleObjectMath.AngleToward(
                    _precisePosition, frame.Player.Position);
                _precisePosition += OracleObjectMath.VectorFromAngle32(angle) * 0.5f;
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                if ((Position - frame.Player.Position).LengthSquared() <= 16.0f)
                {
                    _state = MotionState.Falling;
                    _speedZ = 0;
                }
                break;
            }

            case MotionState.Falling:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x08) ||
                    ((Position - frame.Player.Position).LengthSquared() <= 36.0f &&
                     _zFixed >= -0x0600))
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
                    Position = frame.Player.Position + new Vector2(0, -14);
                    _precisePosition = Position;
                    _zFixed = 0;
                    _state = MotionState.Held;
                }
                break;

            case MotionState.Held when _heldBy is not null:
                Position = _heldBy.Position + new Vector2(0, -14);
                _precisePosition = Position;
                break;
        }

        if (_swirl)
            UpdateSwirl();
        QueueRedraw();
    }

    internal void StartEnergySwirl()
    {
        _swirl = true;
        _beadsInitialized = false;
        Array.Fill(_beadVisible, false);
        Array.Fill(_beadDelay, 0);
        QueueRedraw();
    }

    internal void StopEnergySwirl()
    {
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
        Vector2 pedestalOffset = _record.Position - Position + new Vector2(-16, -16);
        DrawTexture(_pedestal.CurrentTexture, pedestalOffset + transition);

        if (_collected)
            return;

        int z = _zFixed >> 8;
        Vector2 itemOffset = new Vector2(-16, -16 + z) + transition;
        DrawTexture(_glow.CurrentTexture, itemOffset);
        DrawTexture(_essence.CurrentTexture, itemOffset);

        if (!_swirl)
            return;
        for (int index = 0; index < _beads.Length; index++)
        {
            if (!_beadVisible[index])
                continue;
            DrawTexture(
                _beads[index].CurrentTexture,
                _beadPosition[index] + new Vector2(-16, -16) + transition);
        }
    }

    private EnemyAnimationPlayer Load(
        VisualRecord visual,
        int animation)
    {
        var player = new EnemyAnimationPlayer(this, visual.Animations.Length);
        player.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette);
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
        Vector2 delta = player.Position - Position;
        return delta.Y >= 0 && Mathf.Abs(delta.X) < 4 && delta.Length() < 20;
    }

    private void UpdateSwirl()
    {
        // createEnergySwirlGoingIn allocates eight ascending part slots while
        // assigning indices $07 down through $00. Parts therefore consume
        // their shared RNG delays in descending index order every update.
        if (!_beadsInitialized)
        {
            for (int index = _beads.Length - 1; index >= 0; index--)
                _beadDelay[index] = (_random.Next().Value & 0x07) + 1;
            _beadsInitialized = true;
        }

        for (int index = _beads.Length - 1; index >= 0; index--)
        {
            if (!_beadVisible[index])
            {
                _beadDelay[index]--;
                if (_beadDelay[index] != 0)
                    continue;

                _beadVisible[index] = true;
                _beadPosition[index] =
                    OracleObjectMath.VectorFromAngle32(index * 4) * 56.0f;
                _beads[index].SetAnimation(index);
                continue;
            }

            _beadPosition[index] += OracleObjectMath.VectorFromAngle32(
                (index * 4) ^ 0x10) * 3.0f;
            _beads[index].Advance();
            if (_beads[index].CurrentParameter == 0xff)
            {
                _beadVisible[index] = false;
                _beadDelay[index] = (_random.Next().Value & 0x07) + 1;
            }
        }
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
