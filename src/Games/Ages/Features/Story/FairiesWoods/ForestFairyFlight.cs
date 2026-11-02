
namespace Oracles;

/// <summary>
/// Native INTERAC_FOREST_FAIRY $49:$00/$03/$04 movement. Coordinates and velocities
/// retain the original unsigned high-byte checks and signed 8.8 word wrapping.
/// </summary>
internal sealed class ForestFairyFlight : NpcCharacterRoomEntityAdapter,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly FairiesWoodsDatabase _database;
    private readonly FairiesWoodsEventRecord _event;
    private readonly OracleRuntimeState _runtime;
    private readonly OracleSoundEngine _sound;
    private readonly Action<Vector2> _spawnSparkle;
    private readonly Action<Vector2> _spawnPuff;
    private int _presetIndex;
    private int _yFixed;
    private int _xFixed;
    private int _targetY;
    private int _targetX;
    private int _angle;
    private int _direction;
    private int _counter1;
    private int _counter2;
    private int _sparkleCounter = 0x5a;
    private FairyFlightStage _stage = FairyFlightStage.FirstLeg;
    private bool _stateZeroPending = true;
    private readonly int _subId;
    private int _circleSteps;

    internal NpcCharacter Actor { get; }
    internal bool Active { get; private set; } = true;
    public bool Finished => !Active;
    public bool NativeInitializationPending => _stateZeroPending && Active;
    public bool UpdatesDuringDialogue => _stateZeroPending;
    public bool UpdatesDuringRoomEntityFreeze => _stateZeroPending;
    internal int PresetIndex => _presetIndex;
    internal int Angle => _angle;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int SparkleCounter => _sparkleCounter;
    internal int YFixed => unchecked((short)_yFixed);
    internal int XFixed => unchecked((short)_xFixed);
    internal int Stage => (int)_stage;

    internal ForestFairyFlight(
        FairiesWoodsDatabase database,
        OracleRuntimeState runtime,
        OracleSoundEngine sound,
        Action<Vector2> spawnSparkle,
        NpcCharacter actor,
        int presetIndex,
        Action<Vector2> spawnPuff,
        int subId = 0) : base(actor, actor.SetTransitionDrawOffset)
    {
        _database = database;
        _event = database.Event;
        _runtime = runtime;
        _sound = sound;
        _spawnSparkle = spawnSparkle;
        _spawnPuff = spawnPuff;
        Actor = actor;
        _presetIndex = presetIndex;
        _subId = subId;
        if (subId == 4) _stage = FairyFlightStage.WaitForSignal;
        LoadPreset(setPosition: true);
    }

    internal static ForestFairyFlight? TryCreate(RoomEventContext context, FairiesWoodsDatabase data,
        int preset, int subId, string name) => context.Entities.TryAllocateInteraction(() =>
    {
        var movement = data.Movements[preset]; var visual = data.Event;
        var record = new NpcRecord(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id,
            AgesInteractionId.ForestFairy, subId, movement.InitialY, movement.InitialX, preset, 0,
            visual.FairySprite, visual.FairyTileBase, 0, 0, false,
            visual.Animation0, visual.Animation0, visual.Animation0, visual.Animation0, string.Empty,
            NpcImplementationClassification.EventOwned);
        var actor = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
        actor.Name = name; actor.SetAnimationRate(0); actor.SetBlocksLink(false); actor.SetCollisionRadii(4, 4);
        return new ForestFairyFlight(data, context.Entities.RuntimeState, context.Sound,
            position => context.Entities.TryCreateInteraction(new InteractionSparkleSpawn(position, 0,
                new(AgesGameContent.AssetRoot, visual.SparkleSprite, visual.SparkleTileBase, visual.SparklePalette, visual.SparkleAnimation), Subid: 2)),
            actor, preset, position => context.Entities.TryCreateInteraction(new PuzzlePuffSpawn(position, visual.PuffSound)), subId);
    });

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance(frame.Counter);
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (_stateZeroPending) InitializeState();
        return Actor.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void InitializeState()
    {
        if (!_stateZeroPending || !Active) return;
        _stateZeroPending = false;
        Actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex); // $49: objectSetVisiblec1
    }

    private void Advance(int globalFrame)
    {
        if (!Active)
            return;
        if (_stateZeroPending)
        {
            InitializeState();
            return;
        }

        switch (_stage)
        {
            case FairyFlightStage.FirstLeg:
                if (UpdateFlight(globalFrame))
                {
                    if (_subId == 3)
                    {
                        _counter1 = _counter2 = 2;
                        _circleSteps = 0x20;
                        _stage = FairyFlightStage.Circle;
                    }
                    else { SnapToTarget(); _stage = FairyFlightStage.WaitForSignal; }
                }
                break;

            case FairyFlightStage.WaitForSignal:
                if (Signal != 0)
                {
                    Actor.AdvanceAnimationUpdates(1);
                    break;
                }
                if (_subId == 3)
                {
                    _presetIndex++;
                    _stage = FairyFlightStage.SecondLeg;
                    LoadPreset(setPosition: false);
                    break;
                }
                if (_subId == 4)
                {
                    _stage = FairyFlightStage.SecondLeg;
                    LoadPreset(setPosition: true);
                    break;
                }
                if (_presetIndex >= 6)
                {
                    SpawnPuffAndDelete();
                    break;
                }
                _presetIndex += 6;
                _stage = FairyFlightStage.SecondLeg;
                LoadPreset(setPosition: true);
                break;

            case FairyFlightStage.SecondLeg:
                if (UpdateFlight(globalFrame))
                {
                    Delete();
                    break;
                }
                if (PositionY >= 0x80 || PositionX >= 0xa0)
                {
                    Signal = unchecked((byte)(Signal + 1));
                    Delete();
                }
                break;

            case FairyFlightStage.Circle:
                _sparkleCounter = (_sparkleCounter - 1) & 255;
                if ((_sparkleCounter & 7) == 0) _spawnSparkle(Actor.Position);
                if (--_counter2 == 0)
                {
                    _counter2 = _counter1;
                    _angle = (_angle + 1) & ObjectAngle.Mask;
                    if (--_circleSteps == 0)
                    {
                        Signal = unchecked((byte)(Signal + 1));
                        _stage = FairyFlightStage.WaitForSignal;
                        break;
                    }
                }
                ApplySpeed();
                if ((globalFrame & 31) == 0) _sound.PlaySound(_event.MagicSound);
                Actor.AdvanceAnimationUpdates(1);
                break;

        }
    }

    internal void Deactivate()
    {
        if (!Active)
            return;
        Active = false;
        if (GodotObject.IsInstanceValid(Actor))
            Actor.SetActive(false);
    }

    private bool UpdateFlight(int globalFrame)
    {
        if (WithinFour(PositionX, _targetX) &&
            WithinFour(PositionY, _targetY))
        {
            Signal = unchecked((byte)(Signal + 1));
            return true;
        }

        _sparkleCounter = unchecked((byte)(_sparkleCounter - 1));
        bool spawnSparkle = (_sparkleCounter & 7) == 0;
        if (_sparkleCounter == 0)
        {
            _sparkleCounter = 0x5a;
            // forestFairy_subid00State1's SRL carry-clear branch skips
            // the sparkle; only rounding an odd counter falls through.
            spawnSparkle = (_counter2 & 1) != 0;
            _counter2 = (_counter2 >> 1) + (_counter2 & 1);
        }
        if (spawnSparkle)
            _spawnSparkle(Actor.Position);

        _counter1--;
        if (_counter1 == 0)
        {
            _counter1 = _counter2;
            int target = OracleObjectMovement.Shared.RelativeAngle(
                (byte)PositionY,
                (byte)PositionX,
                (byte)_targetY,
                (byte)_targetX);
            int difference = (_angle - target) & ObjectAngle.Mask;
            if (difference != 0)
            {
                _angle = difference < 0x10
                    ? (_angle - 1) & ObjectAngle.Mask
                    : (_angle + 1) & ObjectAngle.Mask;
            }
        }

        ApplySpeed();
        if ((globalFrame & 0x1f) == 0)
            _sound.PlaySound(_event.MagicSound);
        Actor.AdvanceAnimationUpdates(1);
        return false;
    }

    private void LoadPreset(bool setPosition)
    {
        if (_presetIndex < 0 || _presetIndex >= _database.Movements.Count)
        {
            throw new InvalidOperationException(
                $"Forest fairy requested movement preset ${_presetIndex:x2}.");
        }
        FairiesWoodsMovementRecord movement =
            _database.Movements[_presetIndex];
        if (setPosition)
        {
            _yFixed = (movement.InitialY << 8) | (_yFixed & 0xff);
            _xFixed = (movement.InitialX << 8) | (_xFixed & 0xff);
        }
        _targetY = movement.TargetY;
        _targetX = movement.TargetX;
        _angle = movement.Angle;
        _counter1 = movement.Counter;
        _counter2 = movement.Counter;
        _direction = movement.Direction;
        Actor.SetBasePalette(movement.Palette);
        Actor.SetScriptAnimation(
            movement.Direction == 0 ? _event.Animation0 : _event.Animation1);
        UpdateActorPosition();
    }

    private void ApplySpeed()
    {
        OracleObjectPosition position = NativeObjectMovement.ApplySpeed(_runtime,
            new OracleObjectPosition(
                unchecked((ushort)_yFixed),
                unchecked((ushort)_xFixed)),
            0x50,
            _angle);
        _yFixed = position.YFixed;
        _xFixed = position.XFixed;
        UpdateActorPosition();
    }

    private void SnapToTarget()
    {
        _yFixed = (_targetY << 8) | (_yFixed & 0xff);
        _xFixed = (_targetX << 8) | (_xFixed & 0xff);
        UpdateActorPosition();
    }

    private void UpdateActorPosition()
    {
        Actor.Position = new Vector2(PositionX, PositionY);
        Actor.SetScriptDrawOffset(new Vector2(0, -4));
    }

    private void SpawnPuffAndDelete()
    {
        _spawnPuff(Actor.Position);
        Delete();
    }

    private void Delete()
    {
        Active = false;
        if (GodotObject.IsInstanceValid(Actor))
            Actor.SetActive(false);
    }

    private byte Signal
    {
        get => _runtime.ReadWramByte(_event.SignalAddress);
        set => _runtime.SetWramByte(_event.SignalAddress, value);
    }

    private int PositionY => (byte)(_yFixed >> 8);
    private int PositionX => (byte)(_xFixed >> 8);

    private static bool WithinFour(int current, int target) =>
        unchecked((byte)(current - target + 4)) < 9;

}

internal enum FairyFlightStage
{
    FirstLeg,
    WaitForSignal,
    SecondLeg,
    Circle
}
