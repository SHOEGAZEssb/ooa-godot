using Godot;
using System;

namespace oracleofages;

internal partial class BallChainSoldierCharacter : EnemyCharacter, ISwitchHookEnemy
{
    private readonly BallChainBehaviorProfile _data = EnemyBehaviorTables.Shared.BallChain;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _hitPending;
    private bool _collisionEnabled = true;
    private int _initialHealth;
    private int _speedZ;
    private bool _propertiesLoaded;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal bool PendingHit => _hitPending;
    internal int State { get; private set; }
    internal int Counter1 { get; private set; }
    internal int BallSignal { get; private set; }
    internal int ReturnState { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; set; }
    internal int SwitchHookSubstate { get; private set; }
    internal override bool CollisionEnabled => _propertiesLoaded && !IsDead && !IsFallingIntoHole &&
        !DiedInHazard && !GaleCollisionDisabled && _collisionEnabled && State != 3;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition definition, OracleRoomData room, Vector2 position, OracleRandom random)
    {
        if (definition.Id != 0x4b || definition.SubId != 0)
            throw new NotSupportedException("ballAndChainSoldier.s: handler requires ENEMY $4b:$00.");
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(definition), positionedOam: true);
        Record = definition;
        _initialHealth = definition.Health;
        _random = random;
        _movement = new(this, room);
        ConfigureHazards(room, zPosition: () => ZFixed);
        Visible = false;
    }

    internal void UpdateFrame(Vector2 linkPosition, Vector2 enemyTarget, int freeEnemySlots, Action spawnWeapon)
    {
        if (IsDead) return;
        try
        {
            // enemyStandardUpdate reloads state-zero properties and consumes
            // var3d RNG on every retry of the clean-US enemy-slot check.
            if (State == 0)
            {
                Health = _initialHealth;
                _propertiesLoaded = true;
                _collisionEnabled = true;
                RestartAnimation(0);
                _random.Next();
            }
            bool justHit = _hitPending;
            _hitPending = false;
            // enemyStandardUpdate tests only the low seven bits before
            // decrementing. The terminal high bit is retained, not another
            // $80 updates of recoil. This handler ignores recoil movement.
            bool recoil = (KnockbackCounter & 0x7f) != 0;
            if (State != 0 && !justHit && !recoil && Health == 0) { Finish(); return; }
            if (State != 0 && !justHit && recoil) KnockbackCounter--;
            if (ContinueHazard() || CheckHazards()) return;
            switch (State)
            {
                case 0:
                    if (freeEnemySlots < _data.RequiredEnemySlots) return;
                    spawnWeapon();
                    State = ReturnState = 8;
                    Visible = true;
                    ZIndex = NpcCharacter.BehindLinkZIndex;
                    return;
                case 3:
                    if (SwitchHookSubstate == 0) SwitchHookSubstate = 1;
                    else if (SwitchHookSubstate == 3)
                    {
                        int z = ZFixed;
                        if (OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, 0x20)) State = ReturnState;
                        ZFixed = z;
                    }
                    return;
                case 8:
                    if (LinkWithinDistance(linkPosition))
                    {
                        State = ReturnState = 9;
                        Counter1 = _data.WindupFrames;
                        BallSignal++;
                        RestartAnimation(1);
                        return;
                    }
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position, enemyTarget);
                    _movement.MoveAtAngle(Angle, _data.SpeedRaw, allowHoles: false);
                    AdvanceAnimation();
                    return;
                case 9:
                    if (--Counter1 != 0) { AdvanceAnimation(); return; }
                    Counter1 = 1;
                    State = ReturnState = 10;
                    BallSignal++;
                    return;
                case 10:
                    if (Counter1 != 0) return;
                    if (LinkWithinDistance(linkPosition))
                    {
                        State = ReturnState = 9;
                        Counter1 = _data.WindupFrames;
                        BallSignal--;
                    }
                    else
                    {
                        State = ReturnState = 8;
                        BallSignal = 0;
                        RestartAnimation(0);
                    }
                    return;
                default: throw new NotSupportedException($"ballAndChainSoldier.s: ENEMY $4b state ${State:x2} is not represented.");
            }
        }
        finally { AdvanceInvincibilityCounter(); QueueRedraw(); }
    }

    private bool LinkWithinDistance(Vector2 link)
    {
        Vector2 here = OracleObjectMath.ToPixelPosition(Position);
        Vector2 there = OracleObjectMath.ToPixelPosition(link);
        return Math.Abs(here.X - there.X) + Math.Abs(here.Y - there.Y) < _data.AttackDistance;
    }

    internal void FinishBallThrow() => Counter1 = 0;
    internal void MarkContact() => _hitPending = true;
    internal void ProtectFromBallBlock()
    { if (InvincibilityCounter == 0) InvincibilityCounter = _data.ParentBlockInvincibility; }

    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!CollisionEnabled || InvincibilityCounter != 0 || _hitPending) return false;
        Health = Math.Max(0, Health - Math.Max(1, damage));
        if (Health == 0) _collisionEnabled = false;
        _hitPending = true;
        ApplySwordNoKnockback(origin, EnemyKnockbackStrength.Normal);
        return true;
    }

    internal bool TakeSomariaHit(Vector2 origin, int damage)
    {
        if (!TakeSwordHit(origin, damage)) return false;
        // Unlike this enemy's sword effect$0b, Somaria effect$2f writes the
        // ENEMYDMG_04 counters. Its handler still runs AI without recoil motion.
        var profile = EnemyBehaviorTables.Shared.EnemySwordDamageProfiles[1];
        InvincibilityCounter = profile.First;
        KnockbackCounter = profile.Second;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), origin.Floor()) ^ 0x10;
        return true;
    }

    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && !DiedInHazard && State == 3 && SwitchHookSubstate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition) ^ 0x10;
        State = 3; SwitchHookSubstate = 0;
    }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    { Position = position.Floor() + Position - Position.Floor(); ZFixed = (zHigh << 8) | (ZFixed & 0xff); QueueRedraw(); }
    public void SwapSwitchHook() => SwitchHookSubstate = 2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) SwitchHookSubstate = 3; }
}
