using Godot;
using System;

namespace Oracles;

internal partial class GhiniCharacter : EnemyCharacter
{
    internal override bool InitializationPending => _state == GhiniState.Uninitialized;

    private readonly GhiniBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Ghini;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private int _counter;
    private int _angle;
    private GhiniState _state;
    private int _speed;
    private bool _spawnCollisionEnabled;
    private Action? _killSpawnedGhinis;
    private int _stunCounter, _speedZ;
    internal int ZFixed { get; set; }
    internal int StunCounter => _stunCounter;
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;
    internal int Counter { get => _counter; set => _counter = value; }
    internal int Speed => _speed;
    internal override bool CollisionEnabled => Record.SubId != 1
        ? base.CollisionEnabled : _spawnCollisionEnabled && Health > 0 && !GaleCollisionDisabled && !IsDead;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Angle => _angle;
    internal GhiniState State => _state;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action? killSpawnedGhinis = null)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _random = random;
        _killSpawnedGhinis = killSpawnedGhinis;
        _room = room;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary);
        Visible=false;
    }

    internal void UpdateFrame(int frame = 0, Vector2 target = default)
    {
        if (IsDead)
            return;
        bool stunned = Record.SubId == 1 && _state != GhiniState.Uninitialized && !NativeHitPending &&
            !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position,(int)_state,frame,ref _stunCounter,ref z,ref _speedZ);
            ZFixed = _stunCounter == 0 ? unchecked((short)(0xfe00 | (z & 255))) : z;
        }
        if (BeginFrame() || stunned)
            return;
        if (Health == 0) { if (Record.SubId != 1) _killSpawnedGhinis?.Invoke(); Finish(); return; }
        if (_state == GhiniState.Uninitialized)
        {
            _random.Next(); // enemyStandardUpdate initializes var3d before species dispatch.
            _state = GhiniState.Choosing;
            _speed = _behavior.SpeedRaw;
            ZFixed = -512;
            Visible=true;
            if (Record.SubId == 1)
            {
                _state = GhiniState.Spawning;
                _counter = EnemyBehaviorTables.Shared.SpawnGhiniState[0].Value;
                _angle = EnemyBehaviorTables.Shared.SpawnGhiniState[1].Value;
            }
            return;
        }
        if (Record.SubId == 1) { UpdateSpawned(frame, target); return; }
        if (_state == GhiniState.Choosing)
        {
            ChooseDirection();
            _state = GhiniState.Moving;
            return;
        }
        Move();
        _counter--;
        if (_counter == 0)
            _state = GhiniState.Choosing;
        AdvanceAnimation();
        QueueRedraw();
    }

    private void Move()
    {
        Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),
            _speed, _angle).PrecisePosition;
        int previousAngle=_angle;
        // ghini_updateMovement changes only angle through the shared
        // side-view probes. Collision radii do not clamp its coordinates.
        _angle=EnemyAdjacentWallResolver.Shared.BounceAngle(Position,_angle,
            point=>_room.GetTerrainInfo(point).Collision==0xff);
        if (_angle!=previousAngle)
            SetAnimation(_angle < 0x10 ? 1 : 0);
    }

    private void UpdateSpawned(int frame, Vector2 target)
    {
        var data = EnemyBehaviorTables.Shared;
        var state = data.SpawnGhiniState;
        switch (_state)
        {
            case GhiniState.Spawning:
                if (--_counter != 0)
                {
                    if ((_counter & 1) == 0) Visible = !Visible;
                    return;
                }
                Visible = true; _spawnCollisionEnabled = true;
                Accelerate(); break;
            case GhiniState.Wandering:
                Move();
                if ((frame & 1) != 0)
                {
                    _counter = (_counter - 1) & 255;
                    if (_counter == 0) { _state = GhiniState.Slowing; break; }
                    if (_random.Next().Value < state[4].Value)
                    {
                        var roll = _random.Next();
                        int b = roll.High & state[5].Value, c = roll.Low & state[5].Value;
                        // ecom_randomBitwiseAndBCE clears A; OR B tests only
                        // the high random byte, despite the source comment.
                        _angle = b == 0 ? OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), target) : c;
                        SetAnimation(_angle < 16 ? 1 : 0);
                    }
                }
                break;
            case GhiniState.Slowing:
                _counter = (_counter + 1) & 255;
                if (_counter >= state[3].Value) { _counter = state[3].Value; _state = GhiniState.Resting; }
                else { Move(); SetCycleSpeed(); }
                break;
            case GhiniState.Resting:
                if (--_counter == 0) Accelerate();
                break;
            case GhiniState.Accelerating:
                if (--_counter != 0) { Move(); SetCycleSpeed(); }
                else { _state = GhiniState.Wandering; _counter = (_random.Next().Value & state[2].Value) + state[2].Value; }
                break;
            default: throw new InvalidOperationException($"ghini.s: $17:$01 state ${(int)_state:x2}.");
        }
        AdvanceAnimation();
        void Accelerate() { _state = GhiniState.Accelerating; _counter = state[2].Value; _speed = data.SpawnGhiniSpeeds[2].Value; }
        void SetCycleSpeed() => _speed = data.SpawnGhiniSpeeds[_counter < state[6].Value ? 0 : _counter < state[7].Value ? 1 : 2].Value;
    }

    // ghini.s:@dead branches straight to enemyDie for subid $01. It is
    // the other variants' deaths which kill all live $01 siblings.
    protected override void CompleteKnockbackDeath()
    {
        if (Record.SubId != 1) _killSpawnedGhinis?.Invoke();
        base.CompleteKnockbackDeath();
    }

    internal void KillFromParentDeath() { Health = 0; _spawnCollisionEnabled = false; }

    public override void _Draw()
    {
        if (IsDead || !Visible)
            return;
        DrawSetTransform(GaleCollisionDisabled ? Vector2.Zero : Vector2.Down * (ZFixed >> 8));
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void ChooseDirection()
    {
        OracleRandomResult result = _random.Next();
        _counter = _behavior.MoveCounterBase +
            (result.Low & _behavior.MoveCounterMask);
        _angle = result.High & 0x18;
        SetAnimation(_angle < 0x10 ? 1 : 0);
    }

    internal void BeginSeedStatus(bool ember, int damage)
    {
        var data = EnemyBehaviorTables.Shared.TargetGhini;
        var status = ember ? data.EmberStatus : data.PegasusStatus;
        if (ember) { Health = Math.Max(0, Health-damage); DeferNativeHitStatus(); }
        InvincibilityCounter = unchecked((sbyte)status[1].Value); _stunCounter = status[3].Value;
    }
    internal void CompleteBurn() { InvincibilityCounter = 0; _stunCounter = 1; }
    internal void EnterGaleState() { _state = (GhiniState)5; _stunCounter = 0; }
    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!base.TakeSwordHit(origin, damage)) return false;
        if (Record.SubId == 1) DeferNativeHitStatus();
        return true;
    }
}

internal enum GhiniState
{
    Uninitialized,
    Choosing,
    Moving,
    Spawning = 8,
    Wandering = 9,
    Slowing = 10,
    Resting = 11,
    Accelerating = 12
}
