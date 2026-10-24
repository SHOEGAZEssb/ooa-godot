using Godot;
using System;

namespace oracleofages;

// ghini.s:ghini_subid02. Targets and movement use byte coordinates and 8.8
// positions; this variant never checks terrain or bounces during ordinary flight.
internal sealed partial class TargetGhiniCharacter : EnemyCharacter
{
    private readonly TargetGhiniBehaviorProfile _data = EnemyBehaviorTables.Shared.TargetGhini;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private OracleRuntimeState _memory = null!;
    private int _stunCounter, _speedZ;
    private bool _collisionDisabled;
    private Action? _killSpawnedGhinis;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; set; }
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; set; }
    internal Vector2 TargetPosition { get; private set; }
    internal int StunCounter => _stunCounter;
    internal int SpeedZ => _speedZ;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => !_collisionDisabled && Health > 0 && base.CollisionEnabled;
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, OracleRuntimeState memory, Action? killSpawnedGhinis = null)
    {
        if (record.SubId != 2) throw new NotSupportedException($"ghini.s: target handler requires ENEMY$17:$02, got ${record.SubId:x2}.");
        Record = record; _room = room; _random = random; _memory = memory;
        _killSpawnedGhinis = killSpawnedGhinis;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.ScreenBoundary, nativeSpeed: () => Speed);
        Visible = false;
    }

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate initializes var3d before state0.
        State = 8; Speed = _data.InitialSpeed; ZFixed = _data.InitialZ;
        Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
    }

    internal void UpdateFrame(int frame)
    {
        if (IsDead) return;
        bool stunned = State != 0 && !NativeHitPending && !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position, State, frame, ref _stunCounter, ref z, ref _speedZ);
            ZFixed = _stunCounter == 0 ? unchecked((short)((_data.InitialZ & 0xff00) | (z & 0xff))) : z;
        }
        if (BeginFrame() || stunned) return;
        if (Health == 0) { _killSpawnedGhinis?.Invoke(); Finish(); return; }
        switch (State)
        {
            case 0: PrepareForScreenTransition(); return;
            case 8:
                ChooseTarget();
                goto case 9; // state8 falls through and decrements $24 now.
            case 9:
                Dec();
                if (Counter == 0) State = 10;
                else if ((Counter & _data.SpeedStepMask) == 0) Speed = (Speed + _data.SpeedStep) & 0xff;
                goto case 10;
            case 10:
                int dx = (OracleObjectPosition.HighByte(Position.X) - (int)TargetPosition.X + 1) & 0xff;
                int dy = (OracleObjectPosition.HighByte(Position.Y) - (int)TargetPosition.Y + 1) & 0xff;
                if (dx < _data.TargetDiameter && dy < _data.TargetDiameter)
                {
                    State = 11; Counter = _data.DecelerationUpdates;
                    goto case 11; // arrival immediately decrements $1c.
                }
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), TargetPosition);
                Move(); UpdateDirection(); AdvanceAnimation(); return;
            case 11:
                Dec();
                if (Counter == 0) { ChooseTarget(); goto case 9; }
                if ((Counter & _data.SpeedStepMask) == 0) Speed = (Speed - _data.SpeedStep) & 0xff;
                Move(); AdvanceAnimation(); return;
            default: throw new NotSupportedException($"ghini.s: ENEMY$17:$02 state${State:x2}.");
        }
    }
    private void Dec() => Counter = (Counter - 1) & 0xff;
    private void Move()
    {
        Vector2 position = Position;
        NativeObjectMovement.ApplySpeed(_memory, ref position, Speed, Angle);
        Position = position;
    }
    private void ChooseTarget()
    {
        State = 9; Speed = _data.TargetSpeed; Counter = _data.AccelerationUpdates;
        var roll = _random.Next();
        int y = Math.Max(0, (roll.High & _data.TargetMask) - _data.TargetYClamp);
        TargetPosition = new((_room.Width / 2 + (roll.Low & _data.TargetMask) - _data.TargetXOffset) & 0xff,
            (_room.Height / 2 + y - _data.TargetYOffset) & 0xff);
    }
    private void UpdateDirection()
    {
        int direction = Angle < ObjectAngle.HalfTurn ? 1 : 0;
        if (AnimationIndex != direction) RestartAnimation(direction);
    }
    internal void BeginSeedStatus(bool ember, int damage)
    {
        var status = ember ? _data.EmberStatus : _data.PegasusStatus;
        if (ember)
        {
            Health = Math.Max(0, Health - damage);
            if (Health == 0) _collisionDisabled = true;
            DeferNativeHitStatus();
        }
        InvincibilityCounter = unchecked((sbyte)status[1].Value);
        _stunCounter = status[3].Value;
    }
    internal void EnterGaleState() { State = 5; _stunCounter = 0; }
    internal void CompleteBurn()
    {
        // flame.s:partCode12 restores health, then writes stunCounter=$01.
        // Its temporary health=$01 must not re-enable a lethally hit enemy.
        InvincibilityCounter = 0; _stunCounter = 1;
    }
    protected override void CompleteKnockbackDeath()
    { _killSpawnedGhinis?.Invoke(); base.CompleteKnockbackDeath(); }
    internal void MarkLinkHit() => DeferNativeHitStatus();
    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!base.TakeSwordHit(origin, damage)) return false;
        DeferNativeHitStatus(); return true;
    }
}
