using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Shared WRAM-style state for one ten-pitch gallery session.</summary>
internal sealed class ShootingGallerySession
{
    internal int Score { get; set; }
    internal int Round { get; set; }
    internal bool BallFinished { get; private set; }
    internal bool IsStrike { get; private set; }
    internal int HitCount { get; private set; }
    internal int HitTargets { get; private set; }
    internal int PendingResult { get; set; } = -1;
    internal bool GameComplete { get; set; }

    internal void BeginBall()
    {
        BallFinished = false;
        IsStrike = false;
        HitCount = 0;
        HitTargets = 0;
    }

    internal void RecordTargetHit(int type)
    {
        if (type is < 0 or > 3 || HitCount >= 2)
            return;
        if (HitCount == 0)
            HitTargets |= 1 << type;
        else
            HitTargets |= (1 << type) << 4;
        HitCount++;
    }

    internal void FinishBall(bool strike)
    {
        IsStrike = strike;
        BallFinished = true;
    }
}

/// <summary>
/// Dynamically created INTERAC_SHOOTING_GALLERY $30:$03. The seven native
/// states preserve its exact counters, RNG calls, layout depletion, and
/// score/result selection.
/// </summary>
internal sealed partial class ShootingGalleryGameController
    : TransitionOffsetNode2D
{
    private readonly List<int> _remainingLayouts = new();
    private ShootingGalleryEventDatabase _database = null!;
    private ShootingGalleryEventRecord _record;
    private ShootingGallerySession _session = null!;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Action<int> _playSound = null!;
    private Func<long> _animationTick = null!;
    private int _state;
    private int _counter;

    internal bool Finished { get; private set; }
    internal int State => _state;
    internal int Counter => _counter;
    internal int RemainingLayouts => _remainingLayouts.Count;

    internal void Initialize(
        ShootingGalleryEventDatabase database,
        ShootingGallerySession session,
        OracleRoomData room,
        OracleRandom random,
        Action<int> playSound,
        Func<long> animationTick)
    {
        _database = database;
        _record = database.Record;
        _session = session;
        _room = room;
        _random = random;
        _playSound = playSound;
        _animationTick = animationTick;
        Position = new Vector2(_record.ControllerX, _record.ControllerY);
        Visible = false;

        _remainingLayouts.Clear();
        for (int index = 0; index < _record.Rounds; index++)
            _remainingLayouts.Add(index);
        _session.Score = 0;
        _session.Round = 0;
        _session.PendingResult = -1;
        _session.GameComplete = false;
        _state = 1;
        // State 0 falls directly into state 1 on the controller's creation
        // update, so the freshly written $78 counter is decremented once.
        _counter = _record.InitialDelay - 1;
        _playSound(_record.WhistleSound);
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;

        switch (_state)
        {
            case 1:
                if (!CounterElapsed())
                    return;
                _session.BeginBall();
                _state = 2;
                _counter = _record.PitchDelay;
                _playSound(_record.BaseballSound);
                return;

            case 2:
                if (!CounterElapsed())
                    return;
                SpawnTargetPuffs(spawns);
                _state = 3;
                _counter = _record.PuffDelay;
                return;

            case 3:
                if (!CounterElapsed())
                    return;
                SetNextTargetLayout();
                _state = 4;
                _counter = _record.LayoutDelay;
                return;

            case 4:
                if (!CounterElapsed())
                    return;
                _session.Round++;
                _state = 5;
                spawns.Add(new ShootingGalleryBallSpawn(
                    _session,
                    Position,
                    UpdateThisFrame: true));
                return;

            case 5:
                if (!_session.BallFinished)
                    return;
                int result = SelectResult();
                ShootingGalleryResultRecord resultRecord =
                    _database.Result(result);
                _session.Score = Math.Clamp(
                    _session.Score + resultRecord.ScoreDelta,
                    0,
                    9999);
                _session.PendingResult = result;
                _state = 6;
                _counter = _record.PitchDelay;
                return;

            case 6:
                return;

            default:
                throw new InvalidOperationException(
                    $"INTERAC_SHOOTING_GALLERY $30:$03 entered state {_state}.");
        }
    }

    /// <summary>
    /// interactionRunScript returns carry inside state 6, and the controller
    /// continues the round/game transition in that same update.
    /// </summary>
    internal void CompleteResultScript()
    {
        if (Finished || _state != 6)
        {
            throw new InvalidOperationException(
                "Shooting-gallery result completed outside controller state 6.");
        }
        if (_session.Round == _record.Rounds)
        {
            _session.GameComplete = true;
            Finished = true;
            return;
        }
        _state = 1;
        _counter = _record.BetweenRoundDelay;
    }

    private bool CounterElapsed()
    {
        if (_counter <= 0)
            throw new InvalidOperationException(
                "Shooting-gallery controller decremented an empty counter.");
        _counter--;
        return _counter == 0;
    }

    private void SpawnTargetPuffs(ICollection<RoomEntitySpawn> spawns)
    {
        for (int index = 0; index < _database.TargetCount; index++)
        {
            spawns.Add(new PuzzlePuffSpawn(
                PointForPackedPosition(_database.Target(index).PackedPosition),
                _record.PoofSound));
        }
    }

    private void SetNextTargetLayout()
    {
        if (_remainingLayouts.Count == 0)
            throw new InvalidOperationException(
                "Shooting-gallery layout depletion underflowed.");
        int selection = _random.Next().Value % _remainingLayouts.Count;
        int layoutIndex = _remainingLayouts[selection];
        _remainingLayouts.RemoveAt(selection);
        IReadOnlyList<byte> layout = _database.Layout(layoutIndex);
        for (int index = 0; index < _database.TargetCount; index++)
        {
            _room.SetPositionTileAndCollision(
                PointForPackedPosition(_database.Target(index).PackedPosition),
                layout[index],
                null,
                _animationTick());
        }
    }

    private int SelectResult()
    {
        if (_session.HitCount == 0)
            return _session.IsStrike ? 21 : 20;
        int first = HighestSetBit(_session.HitTargets & 0x0f);
        if (_session.HitCount == 1)
            return first;
        int second = HighestSetBit((_session.HitTargets >> 4) & 0x0f);
        return (first + 1) * 4 + second;
    }

    private static int HighestSetBit(int value)
    {
        if (value <= 0)
            throw new InvalidOperationException(
                "Shooting-gallery hit count has no target bit.");
        int bit = 0;
        while ((value >>= 1) != 0)
            bit++;
        return bit;
    }

    private static Vector2 PointForPackedPosition(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);
}

internal sealed class ShootingGalleryGameControllerRoomEntity(
    ShootingGalleryGameController controller)
    : RoomEntityAdapter<ShootingGalleryGameController>(
        controller, controller.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        Entity.UpdateFrame(spawns);
    }
}

internal sealed record ShootingGalleryGameControllerSpawn(
    ShootingGallerySession Session) : RoomEntitySpawn;

internal sealed record ShootingGalleryBallSpawn(
    ShootingGallerySession Session,
    Vector2 Position,
    bool UpdateThisFrame = false) : RoomEntitySpawn(UpdateThisFrame);
