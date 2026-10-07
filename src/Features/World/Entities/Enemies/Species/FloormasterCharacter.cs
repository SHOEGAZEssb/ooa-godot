using Godot;
using System;

namespace oracleofages;

// common/enemies/floorMaster.s: one counted spawner, separately allocated hands.
internal sealed partial class FloormasterCharacter : EnemyCharacter, ITerrainShadowSource
{
    private readonly FloormasterBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Floormaster;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Func<FloormasterCharacter>? _parent;
    private Func<bool>? _spawn;
    private bool _collisionEnabled;
    private bool _grabPending;
    private int _bobIndex, _stunCounter, _stunSpeedZ;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; set; }
    internal int Orientation { get; private set; }
    internal int LiveChildren { get; private set; }
    internal int RemainingChildren { get; private set; }
    internal int ChildSubId { get; private set; }
    internal Vector2 LastLinkPosition { get; private set; }
    internal bool RetreatCompleted { get; private set; }
    internal int StunCounter => _stunCounter;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _collisionEnabled && base.CollisionEnabled;
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position,
        OracleRandom random, Func<FloormasterCharacter>? parent = null, Func<bool>? spawn = null)
    {
        Record = record; _room = room; _random = random; _parent = parent; _spawn = spawn;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.ScreenBoundary, nativeSpeed: () => Speed);
        Visible = false;
    }
    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate var3d precedes the handler.
        Counter = _behavior.InitialCounter;
        State = Record.SubId == 0 ? 1 : 8;
        if (Record.SubId != 0) return;
        RemainingChildren = OracleObjectPosition.HighByte(Position.Y) >> 4;
        ChildSubId = (OracleObjectPosition.HighByte(Position.X) >> 4) + 1;
        if (ChildSubId is not (1 or 2))
            throw new NotSupportedException($"ENEMY_FLOORMASTER $35:$00 encoded XH=${(int)Position.X:x2} requests unsupported child ${ChildSubId:x2}.");
    }
    internal void MarkCapture()
    { _collisionEnabled = false; _grabPending = true; DeferNativeHitStatus(); }
    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!TakeDeferredNoKnockbackHit(origin, damage)) return false;
        DeferNativeHitStatus();
        return true;
    }
    internal override bool TakeBurnHit(int damage) => false;
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;
    internal void BeginEmberHit()
    {
        Health = Math.Max(0, Health - 2);
        InvincibilityCounter = -90; _stunCounter = 90;
        if (Health == 0) _collisionEnabled = false;
        DeferNativeHitStatus();
    }
    internal void BeginPegasusHit() { InvincibilityCounter = -16; _stunCounter = 240; }
    internal void EnterGaleState() { State = 5; _stunCounter = 0; }
    internal override void FinishGale() { NotifyParent(killed: true); Finish(); }
    protected override void CompleteKnockbackDeath() { NotifyParent(killed: true); Finish(); }
    private void NotifyParent(bool killed)
    {
        var parent = _parent?.Invoke() ?? throw new InvalidOperationException("floorMaster.s: a child requires its live relatedObj1 spawner.");
        parent.LiveChildren = (parent.LiveChildren - 1) & 0xff;
        if (killed) parent.RemainingChildren = (parent.RemainingChildren - 1) & 0xff;
    }

    internal void UpdateFrame(Player link, int frame)
    {
        if (State == 0) { PrepareForScreenTransition(); return; }
        bool hit = NativeHitPending;
        bool stunned = !hit && !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position, State, frame, ref _stunCounter, ref z, ref _stunSpeedZ);
            ZFixed = z;
        }
        bool consumed = BeginFrame(continueDuringHitAndKnockback: hit);
        if (IsDead || consumed || stunned) return;
        if (hit)
        {
            if (!_grabPending) return;
            _grabPending = false;
            State = 12;
            ZFixed = (_behavior.GrabZ << 8) | (ZFixed & 0xff);
            UpdateAngle(link);
            RestartAnimation(Orientation + 4);
            Position = new(Midpoint(Position.X, link.Position.X) + Position.X % 1,
                Midpoint(Position.Y, link.Position.Y) + Position.Y % 1);
            return;
        }
        if (Health == 0) { NotifyParent(killed: true); Finish(); return; }
        switch (State)
        {
            case 1:
                if (RemainingChildren == 0) { Finish(); return; }
                if (LiveChildren >= RemainingChildren) return;
                Counter = (Counter - 1) & 0xff;
                if (Counter != 0) return;
                Counter = _behavior.RetryCounter;
                if (LiveChildren >= _behavior.MaximumLive || !(_spawn?.Invoke() ?? false)) return;
                LiveChildren++;
                Counter = _behavior.SpawnCounter;
                return;
            case 8: ChoosePosition(link); return;
            case 9:
                if (AnimationParameter != 1) AdvanceAnimation();
                else { State = 10; RestartAnimation(Orientation + 2); }
                return;
            case 10:
                Counter = (Counter - 1) & 0xff;
                if (Counter != 0)
                { ZFixed = (_behavior.HoverZ[Counter >> 2].Value << 8) | (ZFixed & 0xff); return; }
                Counter = _behavior.ChaseCounter;
                _collisionEnabled = true;
                State = 11;
                UpdateAngle(link);
                Speed = (((int)_parent!().Position.X & 0x20) == 0) ? _behavior.NormalSpeed : _behavior.FastSpeed;
                RestartAnimation(Orientation + 2);
                AdvanceAnimation();
                return;
            case 11:
                Counter = (Counter - 1) & 0xff;
                if (Counter == 0)
                {
                    ZFixed &= 0xff;
                    _collisionEnabled = false;
                    State = 13;
                    RestartAnimation(Orientation + 6);
                    return;
                }
                int old = Orientation;
                UpdateAngle(link);
                if (old != Orientation) RestartAnimation(Orientation + 2);
                if ((Counter & 7) == 0)
                { _bobIndex = (_bobIndex + 1) & 7; ZFixed = (_behavior.ChaseZ[_bobIndex].Value << 8) | (ZFixed & 0xff); }
                var walls = EnemyAdjacentWallResolver.Shared.ProbeTopDown(Position, Angle,
                    point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height);
                Vector2 precise = Position;
                EnemyTerrainMovement.ApplyGivenAdjacentWalls(ref precise, Angle, MovementVelocity(Speed, Angle), walls, Speed);
                Position = precise;
                AdvanceAnimation();
                return;
            case 12:
                switch (AnimationParameter)
                {
                    case 1:
                        Animation.ConsumeParameter(); link.Visible = false;
                        Position = link.Position.Floor() + Position - Position.Floor();
                        return;
                    case 2: Animation.ConsumeParameter(); ZFixed &= 0xff; break;
                    case 3: link.CompleteWallmasterGrab(); Visible = false; return;
                }
                AdvanceAnimation();
                return;
            case 13:
                if (AnimationParameter != 3) { AdvanceAnimation(); return; }
                NotifyParent(killed: false);
                RetreatCompleted = true;
                Finish();
                return;
            default: throw new NotSupportedException($"floorMaster.s: ENEMY $35:${Record.SubId:x2} state ${State:x2} is unrepresented.");
        }
    }
    private static int Midpoint(float enemy, float link) =>
        ((int)enemy + (unchecked((sbyte)((int)link - (int)enemy)) >> 1)) & 0xff;
    private void UpdateAngle(Player link)
    {
        if (!link.PatchCollisionsEnabled) return;
        int angle = OracleObjectMovement.Shared.RelativeAngle(Position, link.Position);
        if ((angle & 15) == 0) { Angle = angle; return; }
        Angle = Record.SubId == 1 ? angle & 0x18 : angle;
        Orientation = Angle < 16 ? 1 : 0;
    }
    private void ChoosePosition(Player link)
    {
        var parent = _parent!();
        Vector2 position = link.Position.Floor();
        bool nearby = (((int)position.Y - (int)parent.LastLinkPosition.Y + _behavior.LinkMotionRadius) & 0xff) < _behavior.LinkMotionDiameter &&
            (((int)position.X - (int)parent.LastLinkPosition.X + _behavior.LinkMotionRadius) & 0xff) < _behavior.LinkMotionDiameter;
        int angle = (link.LinkMovementAngle + (nearby ? _random.Next().Value : 0)) & ObjectAngle.Mask;
        for (int distance = _behavior.InitialDistance - _behavior.DistanceStep; distance > 0; distance -= _behavior.DistanceStep)
        {
            var offset = OracleObjectMovement.Shared.CircleArcOffset(distance, angle);
            Position = new(((int)position.X + offset.X) & 0xff, ((int)position.Y + offset.Y) & 0xff);
            int dx = Math.Abs((int)position.X - (int)Position.X);
            if (dx >= _behavior.MaximumXDifference || Position.Y >= _behavior.MaximumY ||
                Position.X >= _room.Width || Position.Y >= _room.Height || _room.GetTerrainInfo(Position).Collision != 0) continue;
            State = 9;
            Counter = _behavior.HoverCounter;
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, position);
            if (Record.SubId == 1) Angle = (Angle + 4) & 0x18;
            Orientation = Angle < 16 ? 1 : 0;
            RestartAnimation(Orientation);
            Visible = true;
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
            break;
        }
        parent.LastLinkPosition = position;
    }
}
