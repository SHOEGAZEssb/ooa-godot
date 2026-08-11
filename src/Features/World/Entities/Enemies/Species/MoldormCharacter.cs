using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_MOLDORM $4f:$00. One placed spawner becomes an uncounted head and
/// two non-colliding tail segments. The runtime keeps the three source
/// objects under one room-count owner while preserving the head's exact 8.8
/// movement and each tail's independent eight-update displacement buffer.
/// </summary>
internal partial class MoldormCharacter : EnemyCharacter
{
    private const int TailCount = 2;

    private readonly MoldormBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Moldorm;
    private readonly byte[,] _tailOffsets = new byte[TailCount, 8];
    private readonly int[] _tailOffsetIndices = new int[TailCount];
    private readonly byte[] _tailLastParentY = new byte[TailCount];
    private readonly byte[] _tailLastParentX = new byte[TailCount];
    private readonly byte[] _tailY = new byte[TailCount];
    private readonly byte[] _tailX = new byte[TailCount];
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyAnimationPlayer _tail1Animation = null!;
    private EnemyAnimationPlayer _tail2Animation = null!;
    private Vector2 _preciseHeadPosition;
    private bool _initialized;
    private int _turnCounter;
    private int _angle;
    private int _angularSpeed;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal bool Initialized => _initialized;
    internal int TurnCounter => _turnCounter;
    internal int Angle => _angle;
    internal int AngularSpeed => _angularSpeed;
    internal int SpeedRaw => _behavior.SpeedRaw;
    internal Vector2 Tail1Position => TailPosition(0);
    internal Vector2 Tail2Position => TailPosition(1);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        if (record.Animations.Length < 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(record), record.Animations.Length,
                "ENEMY_MOLDORM requires head animations 0-7 and tail animations 8-9.");
        }
        if (_behavior.RequiredEnemySlots != 3 ||
            _behavior.TailDelayFrames != 8)
        {
            throw new InvalidOperationException(
                "The imported Moldorm multipart contract is not three slots with eight-update tails.");
        }

        Record = record;
        _room = room;
        _random = random;
        _preciseHeadPosition = position;
        _initialized = false;
        _turnCounter = 0;
        _angle = 0;
        _angularSpeed = _behavior.InitialAngularSpeed;

        EnemyCharacterConfiguration configuration =
            EnemyCharacterConfiguration.FromImported(record);
        InitializeEnemy(position, configuration);
        _tail1Animation = LoadTailAnimation(configuration, 8);
        _tail2Animation = LoadTailAnimation(configuration, 9);
        Visible = false;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true,
            precisePosition: () => _preciseHeadPosition,
            setPrecisePosition: SetPreciseHeadPosition);
        ConfigureHazards(room, animateWhileFallingInHole: false);
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        if (!_initialized)
        {
            PrepareForScreenTransition();
            return;
        }
        if (CheckHazards())
            return;

        if (BeginFrame())
        {
            UpdateTails();
            return;
        }

        _turnCounter--;
        if (_turnCounter == 0)
        {
            _turnCounter = _behavior.TurnCounterFrames;
            _angle = (_angle + _angularSpeed) & _behavior.AngleMask;
            UpdateHeadAnimation();
            if ((_random.Next().Value & _behavior.ReverseRollMask) == 0)
                _angularSpeed = -_angularSpeed;
        }

        int bouncedAngle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position, _angle, IsWallOrHole);
        if (bouncedAngle != _angle)
        {
            _angle = bouncedAngle;
            UpdateHeadAnimation();
        }

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _preciseHeadPosition,
            _behavior.SpeedRaw,
            _angle);
        UpdateTails();
        QueueRedraw();
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (_initialized)
            return ScreenTransitionPresentation.Visible;

        _initialized = true;
        _turnCounter = _behavior.TurnCounterFrames;
        _angle = _random.Next().Value & _behavior.AngleMask;
        _angularSpeed = _behavior.InitialAngularSpeed;
        InitializeTails();
        UpdateHeadAnimation();
        Visible = true;
        QueueRedraw();
        return ScreenTransitionPresentation.Visible;
    }

    public override void _Draw()
    {
        if (!DrawsAnimation)
            return;

        DrawTail(_tail2Animation, 1);
        DrawTail(_tail1Animation, 0);
        DrawCurrentAnimation();
    }

    private EnemyAnimationPlayer LoadTailAnimation(
        EnemyCharacterConfiguration configuration,
        int animation)
    {
        var player = new EnemyAnimationPlayer(
            this, configuration.Animations.Count);
        player.Load(
            configuration.Source,
            configuration.Animations,
            configuration.TileBase,
            configuration.Palette,
            configuration.DamagePalette,
            sourceGrayscaleInverted:
                configuration.SourceGrayscaleInverted);
        player.SetAnimation(animation);
        return player;
    }

    private void InitializeTails()
    {
        byte y = HighY(Position);
        byte x = HighX(Position);
        for (int tail = 0; tail < TailCount; tail++)
        {
            _tailOffsetIndices[tail] = 0;
            _tailLastParentY[tail] = y;
            _tailLastParentX[tail] = x;
            _tailY[tail] = y;
            _tailX[tail] = x;
            for (int frame = 0; frame < _behavior.TailDelayFrames; frame++)
                _tailOffsets[tail, frame] = (byte)_behavior.NeutralTailDelta;
        }
    }

    private void UpdateTails()
    {
        UpdateTail(0, HighY(Position), HighX(Position));
        UpdateTail(1, _tailY[0], _tailX[0]);
        QueueRedraw();
    }

    private void UpdateTail(int tail, byte parentY, byte parentX)
    {
        byte yDelta = unchecked((byte)(
            parentY - _tailLastParentY[tail] + 8));
        byte xDelta = unchecked((byte)(
            parentX - _tailLastParentX[tail] + 8));
        byte packedDelta = (byte)((yDelta << 4) | xDelta);
        _tailLastParentY[tail] = parentY;
        _tailLastParentX[tail] = parentX;

        int index = _tailOffsetIndices[tail];
        _tailOffsets[tail, index] = packedDelta;
        index = (index + 1) & (_behavior.TailDelayFrames - 1);
        _tailOffsetIndices[tail] = index;

        byte delayed = _tailOffsets[tail, index];
        int delayedY = ((delayed >> 4) & 0x0f) - 8;
        int delayedX = (delayed & 0x0f) - 8;
        _tailY[tail] = unchecked((byte)(_tailY[tail] + delayedY));
        _tailX[tail] = unchecked((byte)(_tailX[tail] + delayedX));
    }

    private void UpdateHeadAnimation() =>
        SetAnimation(
            ((_angle + _behavior.AnimationAngleOffset) &
                _behavior.AnimationAngleMask) >> 2);

    private bool IsWallOrHole(Vector2I point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        _room.IsSolid(point) ||
        _room.GetTerrainInfo(point).Hazard == HazardType.Hole;

    private void SetPreciseHeadPosition(Vector2 position)
    {
        _preciseHeadPosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        QueueRedraw();
    }

    private void DrawTail(EnemyAnimationPlayer animation, int tail)
    {
        Texture2D texture = DrawsDamagePalette
            ? animation.DamageTexture
            : animation.CurrentTexture;
        DrawTexture(
            texture,
            animation.CurrentOffset + TailDrawOffset(tail) +
                TransitionDrawOffset);
    }

    private Vector2 TailDrawOffset(int tail)
    {
        byte headY = HighY(Position);
        byte headX = HighX(Position);
        int y = unchecked((sbyte)(byte)(_tailY[tail] - headY));
        int x = unchecked((sbyte)(byte)(_tailX[tail] - headX));
        return new Vector2(x, y);
    }

    private Vector2 TailPosition(int tail) => new(
        _tailX[tail], _tailY[tail]);

    private static byte HighY(Vector2 position) =>
        OracleObjectPosition.HighByte(position.Y);

    private static byte HighX(Vector2 position) =>
        OracleObjectPosition.HighByte(position.X);
}
