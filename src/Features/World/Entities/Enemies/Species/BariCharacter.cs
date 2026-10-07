using Godot;

namespace oracleofages;

// object_code/ages/enemies/bari.s: the shared large Bari/small Biri handler.
internal sealed partial class BariCharacter : EnemyCharacter, ITerrainShadowSource
{
    private readonly BariBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Bari;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Vector2 _origin;
    private int _bobCounter, _pendingCollision, _stunCounter, _stunSpeedZ;
    private bool _collisionEnabled = true;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; private set; }
    internal int CollisionMode { get; private set; }
    internal bool SplitCompleted => State == 10 && Substate != 0 && IsDead;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _collisionEnabled && base.CollisionEnabled;
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    internal int StunCounter => _stunCounter;
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position,
        OracleRandom random, int angle = 0, int zHigh = 0)
    {
        Record = record;
        _room = room;
        _random = random;
        Angle = angle;
        ZFixed = zHigh << 8;
        CollisionMode = _behavior.NormalMode;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, nativeSpeed: () => Speed);
        Visible = false;
    }

    internal void NotifyCollision(int collision)
    {
        _pendingCollision = collision;
        DeferNativeHitStatus();
    }

    internal void ReceiveElectricCollision()
    {
        _collisionEnabled = false;
        NotifyCollision(_behavior.ElectricCollision);
    }

    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!TakeDeferredNoKnockbackHit(origin, damage)) return false;
        NotifyCollision(ItemCollisionType.L1Sword);
        return true;
    }

    internal override bool TakeBurnHit(int _) => false;

    internal BariUpdateEvent UpdateFrame(Vector2 target, int frameCounter)
    {
        bool hit = NativeHitPending;
        bool stunned = State != 0 && !hit && !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position, State, frameCounter, ref _stunCounter, ref z, ref _stunSpeedZ);
            ZFixed = z;
        }
        if (BeginFrame(continueDuringHitAndKnockback: hit) || stunned) return BariUpdateEvent.None;
        if (hit)
        {
            if (Health == 0) return BariUpdateEvent.None;
            if (Record.SubId == 0 && CollisionMode != _behavior.ElectricMode &&
                _pendingCollision >= _behavior.SplitMinimumCollision && _pendingCollision != ItemCollisionType.GaleSeed)
                State = 10;
        }
        if (State == 0)
        {
            PrepareForScreenTransition();
            return BariUpdateEvent.None;
        }
        _bobCounter = (_bobCounter - 1) & 0xff;
        ZFixed = (_behavior.ZValues[(_bobCounter & _behavior.BobMask) >> _behavior.BobShift].Value << 8) | (ZFixed & 0xff);
        if (State == 10)
        {
            if (Substate == 0)
            {
                Substate = 1;
                Counter2 = _behavior.SplitUpdates;
                _collisionEnabled = false;
                Visible = false;
                return BariUpdateEvent.BeginSplit;
            }
            Counter2 = (Counter2 - 1) & 0xff;
            if (Counter2 != 0) return BariUpdateEvent.None;
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target);
            Finish();
            return BariUpdateEvent.SpawnChildren;
        }
        if (State == 9)
        {
            Counter2 = (Counter2 - 1) & 0xff;
            if (Counter2 != 0) AdvanceAnimation();
            else
            {
                State = 8;
                CollisionMode = _behavior.NormalMode;
                _collisionEnabled = true;
                RestartAnimation(0);
                // bari_state9 falls through its CALL to enemySetAnimation
                // into bari_setRandomAngleAndCounter2 (there is no RET).
                SetRandomAngleAndCounter2();
            }
            return BariUpdateEvent.None;
        }
        if (Record.SubId == 0)
        {
            Counter2 = (Counter2 - 1) & 0xff;
            if (Counter2 == 0)
            {
                Counter2 = _behavior.ShockUpdates;
                State = 9;
                CollisionMode = _behavior.ElectricMode;
                RestartAnimation(1);
                return BariUpdateEvent.None;
            }
        }
        Counter1 = (Counter1 - 1) & 0xff;
        if (Counter1 == 0)
        {
            Counter1 = (_random.Next().Value & (Record.SubId == 0 ? _behavior.LargeCounterMask : _behavior.SmallCounterMask)) +
                (Record.SubId == 0 ? _behavior.LargeCounterOffset : _behavior.SmallCounterOffset);
            int angle = OracleObjectMovement.Shared.RelativeAngle(Position, Record.SubId == 0 ? _origin : target);
            int delta = (Angle - angle) & ObjectAngle.Mask;
            if (delta != 0) Angle = (Angle + (delta < 0x10 ? -1 : 1)) & ObjectAngle.Mask;
        }
        var velocity = MovementVelocity(Speed, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
            point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height);
        AdvanceAnimation();
        return BariUpdateEvent.None;
    }

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate's var3d, before species RNG.
        State = 8;
        Counter1 = _behavior.InitialCounter;
        Speed = Record.SubId == 0 ? _behavior.LargeSpeed : _behavior.SmallSpeed;
        ZFixed = (_behavior.ZValues[0].Value << 8) | (ZFixed & 0xff);
        _origin = Position.Floor();
        _bobCounter = _random.Next().Value;
        if (Record.SubId == 0)
            SetRandomAngleAndCounter2();
        else RestartAnimation(2);
        Visible = true;
        ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
    }

    private void SetRandomAngleAndCounter2()
    {
        Counter2 = _behavior.ShockCounters[_random.Next().Value & 3].Value;
        Angle = _random.Next().Value & ObjectAngle.Mask;
    }
}

internal enum BariUpdateEvent { None, BeginSplit, SpawnChildren }
