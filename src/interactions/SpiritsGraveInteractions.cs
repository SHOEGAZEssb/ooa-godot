using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SpiritsGravePuzzleState
{
    internal int CubePosition { get; set; }
    internal int CubeColor { get; set; }
}

internal abstract partial class SpiritsGraveVisualEntity : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;

    protected int AnimationIndex => _animation.AnimationIndex;
    protected int AnimationFrame => _animation.FrameIndex;
    protected int AnimationParameter => _animation.CurrentParameter;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;

    protected void InitializeVisual(
        SpiritsGraveDatabase.VisualRecord visual,
        Vector2 position,
        int animation = 0,
        int? palette = null,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides = null)
    {
        Position = position;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            palette ?? visual.Palette,
            paletteOverrides: paletteOverrides,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        SetAnimation(animation);
    }

    protected void SetAnimation(int animation) => _animation.SetAnimation(animation);
    protected void AdvanceAnimation() => _animation.Advance();

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
            DrawTexture(_animation.CurrentTexture, new Vector2(-16, -16) + TransitionDrawOffset);
    }
}

/// <summary>INTERAC_MOVING_PLATFORM $79, including Link riding displacement.</summary>
internal sealed partial class SpiritsGraveMovingPlatform : SpiritsGraveVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private readonly int _script;
    private readonly Vector2 _collisionRadii;
    private int _state;
    private int _counter;
    private int _moveRemaining;
    private Vector2 _moveDirection;
    private Vector2 _precisePosition;
    private bool _linkRiding;

    public Node2D Node => this;
    internal int Script => _script;
    internal int Counter => _counter;
    internal int MoveRemaining => _moveRemaining;
    internal bool LinkRiding => _linkRiding;
    internal Vector2 CollisionRadii => _collisionRadii;
    internal Vector2 PrecisePosition => _precisePosition;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;

    internal SpiritsGraveMovingPlatform(
        SpiritsGraveDatabase.VisualRecord visual,
        Vector2 position,
        int rawSubId,
        Vector2 collisionRadii)
    {
        _script = rawSubId >> 3;
        _collisionRadii = collisionRadii;
        if (_script is not (0 or 1) ||
            _collisionRadii.X <= 0 ||
            _collisionRadii.Y <= 0)
        {
            throw new InvalidOperationException(
                $"Unsupported D1 moving-platform subid ${rawSubId:x2}.");
        }
        Name = $"SpiritsGraveMovingPlatform_{_script}";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        InitializeVisual(visual, position);
        _precisePosition = position;
        BeginWait();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // interactionCode79 updates wLinkRidingObject before it executes the
        // current wait/move script state. The containment check uses only the
        // objects' high coordinate bytes.
        Vector2 linkPoint = frame.Player.Position + Vector2.Down * 5.0f;
        bool touching =
            Mathf.Abs(linkPoint.X - Position.X) < _collisionRadii.X &&
            Mathf.Abs(linkPoint.Y - Position.Y) < _collisionRadii.Y;
        if (_linkRiding && !touching)
            _linkRiding = false;
        else if (!_linkRiding && touching)
            _linkRiding = true;

        Vector2 displacement = Vector2.Zero;
        if (_counter > 0)
        {
            _counter--;
        }
        else if (_moveRemaining > 0)
        {
            displacement = _moveDirection * 0.5f;
            _precisePosition += displacement;
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _moveRemaining--;
            if (_moveRemaining == 0)
                BeginWait();
        }
        else
        {
            BeginMove();
        }

        if (_linkRiding && displacement != Vector2.Zero &&
            frame.Player.IsGroundedForFloorButton)
        {
            frame.Player.ApplyMovingPlatformDisplacement(displacement);
        }
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void BeginWait()
    {
        _counter = 8;
        _moveRemaining = 0;
    }

    private void BeginMove()
    {
        if (_script == 0)
        {
            _moveDirection = (_state++ & 1) == 0 ? Vector2.Up : Vector2.Down;
            _moveRemaining = 0x80;
            return;
        }

        if (_state == 0)
        {
            _state = 1;
            _moveDirection = Vector2.Left;
            _moveRemaining = 0x40;
        }
        else
        {
            _moveDirection = (_state++ & 1) == 1 ? Vector2.Right : Vector2.Left;
            _moveRemaining = 0x80;
        }
    }
}

internal sealed partial class SpiritsGraveMovingPlatformSpawner : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly Func<int, bool> _triggerActive;
    private readonly Action<int> _playSound;
    private int _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal int Counter => _counter;

    internal SpiritsGraveMovingPlatformSpawner(
        Func<int, bool> triggerActive,
        Action<int> playSound)
    {
        _triggerActive = triggerActive;
        _playSound = playSound;
        Name = "SpiritsGraveMovingPlatformSpawner";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_state == 0)
        {
            if (!_triggerActive(1))
                return;
            spawns.Add(new PuzzlePuffSpawn(new Vector2(0x78, 0x48), 0));
            spawns.Add(new PuzzlePuffSpawn(new Vector2(0x78, 0x58), 0));
            _counter = 30;
            _state = 1;
            return;
        }
        if (--_counter != 0)
            return;
        spawns.Add(new SpiritsGraveMovingPlatformSpawn(
            new Vector2(0x78, 0x50), 0x09));
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}

/// <summary>Native D1 two-torch staircase script in room $4:$1b.</summary>
internal sealed partial class SpiritsGraveTorchStairs : Node2D,
    IRoomEntity, IFixedRoomEntity, ISeedHittableRoomEntity, IRoomEntityLifetime
{
    private readonly SpiritsGraveDatabase.ObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData? _save;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly List<int> _unlit = new();
    private int _pending = -1;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int LitCount { get; private set; }
    internal IReadOnlyList<int> UnlitPositions => _unlit;

    internal SpiritsGraveTorchStairs(
        SpiritsGraveDatabase.ObjectRecord record,
        OracleRoomData room,
        OracleSaveData? save,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _save = save;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = "SpiritsGraveTorchStairs";
        Position = record.Position;
        for (int index = 0; index < room.Layout.Length; index++)
        {
            if (room.Layout[index] == 0x08)
                _unlit.Add((index / 16 << 4) | index % 16);
        }
        if (_unlit.Count != 2)
            throw new InvalidOperationException(
                $"{record.Source} expected two unlit torches, found {_unlit.Count}.");
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_pending >= 0 || Finished)
            return SeedHitResult.None;
        foreach (int packed in _unlit)
        {
            Vector2 center = PositionFromPacked(packed);
            if (hitbox.Intersects(new Rect2(center - new Vector2(6, 6), new Vector2(12, 12))))
            {
                _pending = packed;
                return SeedHitResult.Consume;
            }
        }
        return SeedHitResult.None;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_pending < 0 || Finished)
            return;
        int packed = _pending;
        _pending = -1;
        _unlit.Remove(packed);
        _room.SetPositionTileAndCollision(
            PositionFromPacked(packed), 0x09, null, _animationTick());
        _roomTileChanged();
        _playSound(OracleSoundEngine.SndLightTorch);
        LitCount++;
        if (LitCount != 2)
            return;

        _save?.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80);
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        spawns.Add(new PuzzlePuffSpawn(_record.Position, 0));
        _room.SetPositionTileAndCollision(
            _record.Position, 0x45, null, _animationTick());
        _roomTileChanged();
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public void SetTransitionDrawOffset(Vector2 offset) { }

    private static Vector2 PositionFromPacked(int packed) => new(
        (packed & 0x0f) * 16 + 8,
        (packed >> 4) * 16 + 8);
}

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
        SpiritsGraveDatabase.ObjectRecord record,
        SpiritsGraveDatabase.VisualRecord visual,
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
        OracleRoomData.TerrainInfo terrain = _room.GetTerrainInfo(target);
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

internal sealed partial class SpiritsGraveCubeFlame : SpiritsGraveVisualEntity,
    IRoomEntity, IFixedRoomEntity
{
    private readonly SpiritsGravePuzzleState _puzzle;
    private readonly EnemyAnimationPlayer[] _palettes = new EnemyAnimationPlayer[3];
    private int _palette;

    public Node2D Node => this;
    internal int Palette => _palette;

    internal SpiritsGraveCubeFlame(
        SpiritsGraveDatabase.ObjectRecord record,
        SpiritsGraveDatabase.VisualRecord visual,
        SpiritsGravePuzzleState puzzle)
    {
        _puzzle = puzzle;
        Name = $"SpiritsGraveCubeFlame_{record.Order}";
        Position = record.Position;
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites);
        int[] sourcePalettes = { 2, 3, 1 };
        for (int index = 0; index < _palettes.Length; index++)
        {
            _palettes[index] = new EnemyAnimationPlayer(this, 1);
            _palettes[index].Load(source, visual.Animations, visual.TileBase, sourcePalettes[index]);
            _palettes[index].SetAnimation(0);
        }
        ApplyPuzzleState(advanceAnimation: false);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        ApplyPuzzleState(advanceAnimation: true);
    }

    private void ApplyPuzzleState(bool advanceAnimation)
    {
        Visible = (_puzzle.CubeColor & 0x80) != 0;
        if (!Visible)
            return;
        _palette = _puzzle.CubeColor & 0x7f;
        if (advanceAnimation)
            _palettes[_palette].Advance();
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && _palettes[_palette].HasFrames)
            DrawTexture(_palettes[_palette].CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
    }
}

internal sealed partial class SpiritsGraveCubeSensor : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly SpiritsGravePuzzleState _puzzle;
    private readonly bool _light;
    private readonly int _packedPosition;
    private readonly Action<int, bool> _setTrigger;
    private readonly Action<int> _playSound;
    private int _lastPosition = -1;

    public Node2D Node => this;

    internal SpiritsGraveCubeSensor(
        SpiritsGraveDatabase.ObjectRecord record,
        OracleRoomData room,
        SpiritsGravePuzzleState puzzle,
        Action<int, bool> setTrigger,
        Action<int> playSound)
    {
        _puzzle = puzzle;
        _light = record.Kind == SpiritsGraveDatabase.ObjectKind.CubeLightSensor;
        _packedPosition = _light ? room.GetPackedPosition(record.Position) : 0;
        _setTrigger = setTrigger;
        _playSound = playSound;
        Name = _light ? "SpiritsGraveCubeLightSensor" : "SpiritsGraveCubeTriggerSensor";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_light)
        {
            if (_lastPosition != _puzzle.CubePosition &&
                _puzzle.CubePosition == _packedPosition)
            {
                _puzzle.CubeColor |= 0x80;
                _playSound(OracleSoundEngine.SndLightTorch);
            }
            _lastPosition = _puzzle.CubePosition;
        }
        else
        {
            _setTrigger(0, _puzzle.CubeColor == 0x82);
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}

internal sealed partial class SpiritsGraveRewardController : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly SpiritsGraveDatabase.ObjectRecord _record;
    private readonly OracleSaveData? _save;
    private readonly Func<int> _enemyCount;
    private readonly GroundTreasureDatabase.Record? _treasure;
    private readonly Action _enableLinkCollisionsAndMenu;
    private int _counter = -1;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Counter => _counter;

    internal SpiritsGraveRewardController(
        SpiritsGraveDatabase.ObjectRecord record,
        OracleSaveData? save,
        Func<int> enemyCount,
        GroundTreasureDatabase.Record? treasure,
        Action enableLinkCollisionsAndMenu)
    {
        _record = record;
        _save = save;
        _enemyCount = enemyCount;
        _treasure = treasure;
        _enableLinkCollisionsAndMenu = enableLinkCollisionsAndMenu;
        Name = $"SpiritsGraveReward_{record.Kind}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_record.Kind == SpiritsGraveDatabase.ObjectKind.BraceletReward)
        {
            SpawnTreasure(spawns);
            return;
        }
        if (_enemyCount() != 0)
            return;

        if (_record.Kind == SpiritsGraveDatabase.ObjectKind.EnemySmallKey)
        {
            SpawnTreasure(spawns);
            return;
        }

        if (_counter < 0)
        {
            _save?.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80);
            if (_record.Kind == SpiritsGraveDatabase.ObjectKind.BossReward)
            {
                SpawnTreasure(spawns);
                return;
            }
            _counter = 20;
            return;
        }
        if (--_counter != 0)
            return;
        spawns.Add(new SpiritsGraveMinibossPortalSpawn());
        _enableLinkCollisionsAndMenu();
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void SpawnTreasure(ICollection<RoomEntitySpawn> spawns)
    {
        if (_treasure.HasValue)
            spawns.Add(new GroundTreasureSpawn(_treasure.Value));
        if (_record.Kind == SpiritsGraveDatabase.ObjectKind.BossReward)
            _enableLinkCollisionsAndMenu();
        Finished = true;
    }
}

/// <summary>
/// INTERAC_ESSENCE $7f. The entity owns the original object motion and OAM;
/// the room-event layer owns dialogue, music, flags, and the destination warp.
/// </summary>
internal sealed partial class SpiritsGraveEssence : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker
{
    private enum MotionState
    {
        Waiting,
        Approaching,
        Falling,
        Delay,
        Held
    }

    private readonly SpiritsGraveDatabase.ObjectRecord _record;
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
        SpiritsGraveDatabase.ObjectRecord record,
        SpiritsGraveDatabase.VisualRecord essence,
        SpiritsGraveDatabase.VisualRecord pedestal,
        SpiritsGraveDatabase.VisualRecord glow,
        SpiritsGraveDatabase.VisualRecord bead,
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
        SpiritsGraveDatabase.VisualRecord visual,
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
