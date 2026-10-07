using Godot;
using System;

namespace oracleofages;

// ENEMY_CANDLE $55; the enemy remains the collision owner during INTERAC$56.
internal sealed partial class CandleCharacter : EnemyCharacter
{
    private readonly CandleBehaviorProfile _data = EnemyBehaviorTables.Shared.Candle;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private OracleRuntimeState _memory = null!;
    private EnemyTerrainMovement _movement = null!;
    private Func<bool> _createFlame = null!;
    private Func<Vector2, int> _createExplosion = null!;
    private Func<int, int> _explosionParameter = null!;
    private Action<int, int> _consumeExplosionParameter = null!;
    private bool _emberHit;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ExplosionSlot { get; private set; } = -1;
    internal bool ExplosionCompleted { get; private set; }
    internal int CollisionMode => State == 14 ? _data.ExplosionMode : 0x3e;
    internal override bool InitializationPending => State == 0;
    // objectSetInvisible does not clear collisionType. The source Candle
    // supplies its own radii for the invisible explosion, unlike most enemies.
    internal override bool CollisionEnabled => State != 0 && !IsDead && !DiedInHazard;
    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position,
        OracleRandom random, OracleRuntimeState memory, Func<bool> createFlame, Func<Vector2, int> createExplosion,
        Func<int, int> parameter, Action<int, int> consume)
    {
        Record = record; _room = room; _random = random; _memory = memory;
        _createFlame = createFlame; _createExplosion = createExplosion;
        _explosionParameter = parameter; _consumeExplosionParameter = consume;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        _movement = new(this, room);
        ConfigureHazards(room);
        Visible = false;
    }
    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate: var3d before species dispatch
        Counter = _data.Wait; Speed = _data.WalkSpeed; State = 8;
        Visible = true; ZIndex = ObjectDrawPriority.BehindLinkZIndex;
    }
    internal void NotifyCollision(int collision)
    {
        _emberHit = collision == ItemCollisionType.EmberSeed;
        DeferNativeHitStatus();
    }
    internal bool Bump(Vector2 origin)
    {
        if (!CollisionEnabled || NativeHitPending || InvincibilityCounter != 0) return false;
        ApplyCollisionBump(origin, EnemyKnockbackStrength.Low);
        NotifyCollision(-1);
        return true;
    }
    internal void NotifySeedConsumed(int collision, Vector2 origin)
    {
        KnockbackCounter = 0; // ENEMYDMG_$44 writes counter even though zero
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), origin.Floor()) ^ ObjectAngle.HalfTurn;
        NotifyCollision(collision);
    }
    private void Dec() => Counter = (Counter - 1) & 0xff;
    internal void UpdateFrame()
    {
        bool ember = NativeHitPending && _emberHit;
        if (CheckHazards()) { AdvanceInvincibilityCounter(); return; }
        if (BeginFrame(continueDuringHitAndKnockback: true)) return;
        _emberHit = false;
        if (ember && State < 10) State = 10;
        switch (State)
        {
            case 0: PrepareForScreenTransition(); return;
            case >= 1 and <= 7: return; // candle_state_stub
            case 8:
                Dec();
                if (Counter == 0)
                {
                    State = 9; Counter = _data.Walk;
                    Angle = (_random.Next().Value & _data.AngleMask) + _data.AngleOffset;
                    RestartAnimation(1);
                }
                return;
            case 9:
                Dec();
                if (Counter == 0) { Counter = _data.Wait; State = 8; RestartAnimation(0); }
                _movement.MoveAtAngle(Angle, Speed, allowHoles: false);
                AdvanceAnimation(); return;
            case 10:
                if (!_createFlame()) return;
                State = 11; Counter = _data.Burn; Speed = _data.BurnSpeed; RestartAnimation(2); return;
            case 11:
                Dec();
                if (Counter == 0) { Counter = _data.Burn; State = 12; Speed = _data.FastSpeed; RestartAnimation(3); }
                MoveBurning(); return;
            case 12:
                Dec();
                if (Counter != 0) { MoveBurning(); return; }
                Counter = _data.Flicker; State = 13;
                goto case 13; // C falls into D and decrements again this update
            case 13:
                Visible = !Visible; Dec();
                if (Counter != 0) { MoveBurning(); return; }
                Counter = 1;
                int slot = _createExplosion(Position.Floor());
                if (slot < 0) return;
                ExplosionSlot = slot; State = 14; Visible = false; return;
            case 14:
                int parameter = _explosionParameter(ExplosionSlot);
                if (parameter == 0) return;
                if ((parameter & 0x80) != 0) { ExplosionCompleted = true; Finish(); return; }
                // candle_stateE retains H from objectGetRelatedObject1Var.
                // The radius stores therefore address the ENEMY union on
                // the explosion's page, rather than this Candle's page.
                _consumeExplosionParameter(ExplosionSlot, _data.ExplosionRadius); return;
            default: throw new NotSupportedException($"candle.s: ENEMY$55 state${State:x2}.");
        }
    }
    private void MoveBurning()
    {
        var speed = NativeObjectMovement.Velocity(_memory, Speed, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(speed.YFixed, speed.XFixed).PrecisePosition;
        Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
            point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height ||
                _room.IsSolidForEnemyMovement(point, holesAreWalls: true));
        AdvanceAnimation();
    }
    internal override bool TryApplyShieldBump(Rect2 bounds, Vector2 origin, EnemyKnockbackStrength strength) => Bump(origin);
    internal override bool TakeSwordHit(Vector2 origin, int damage) => false;
    internal override bool TakeBurnHit(int damage) => false;
}
