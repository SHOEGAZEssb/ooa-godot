using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal partial class GibdoCharacter : EnemyCharacter, ISwitchHookEnemy
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _behavior = EnemyBehaviorTables.Shared.Gibdo;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _hitPending;
    private bool _emberPending;
    private bool _hitCollision = true;
    private int _stunCounter;
    internal int StunCounter => _stunCounter;
    internal bool HasPendingHit => _hitPending || _emberPending;
    internal void ClearSeedStun() => _stunCounter = 0;
    internal override bool CollisionEnabled => base.CollisionEnabled && _hitCollision && !Replaced && State != 3;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int SwitchHookSubstate { get; private set; }
    private int _speedZ;
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; set; }
    internal bool Replaced { get; set; }
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + new Vector2(0, ZFixed >> 8);
    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position, OracleRandom random)
    {
        Record = record;
        _random = random;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        _movement = new EnemyTerrainMovement(this, room);
        ConfigureHazards(room, zPosition: () => ZFixed);
    }

    internal bool UpdateFrame(int frameCounter = 0)
    {
        if (IsDead || Replaced) return false;
        if (DiedInHazard) { BeginFrame(); return false; }
        try
        {
            if (CheckHazards()) return false;
            if (State == 0)
            {
                _random.Next(); // enemyStandardUpdate's common var3d write.
                State = 8;
                return false;
            }
            if (_emberPending)
            {
                _emberPending = false;
                _hitPending = false;
                _stunCounter = 0;
                if (State != 10) { State = 10; Counter = _behavior[4].Value; }
                return false;
            }
            if (_hitPending) { _hitPending = false; return false; }
            if (Health == 0) { Finish(); return false; }
            // enemyCode12 returns on non-Ember statuses; the common handler
            // decrements knockback without applying the enemy's velocity.
            if (KnockbackCounter > 0) { KnockbackCounter--; return false; }
            if (_stunCounter > 0)
            {
                int z = ZFixed;
                Position = EnemyStunMotion.Update(Position, State, frameCounter, ref _stunCounter, ref z, ref _speedZ);
                ZFixed = z;
                QueueRedraw();
                return false;
            }
            switch (State)
            {
                case 3:
                    if (SwitchHookSubstate == 0) SwitchHookSubstate = 1;
                    else if (SwitchHookSubstate == 3)
                    {
                        // ecom_fallToGroundAndSetState8 adds speedZ before gravity.
                        ZFixed = (short)(ZFixed + _speedZ);
                        if (ZFixed >= 0) { ZFixed = 0; State = 8; }
                        else _speedZ = (short)(_speedZ + 0x20);
                        QueueRedraw();
                    }
                    break;
                case 8:
                    var roll = _random.Next();
                    Angle = roll.High & _behavior[1].Value;
                    Counter = (roll.Low & _behavior[2].Value) + _behavior[3].Value;
                    State = 9;
                    AdvanceAnimation();
                    break;
                case 9:
                    if (--Counter == 0 || !_movement.MoveUsingAdjacentWalls(Angle, _behavior[0].Value, false, false))
                        State = 8;
                    AdvanceAnimation();
                    break;
                case 10:
                    return --Counter == 0;
            }
            return false;
        }
        finally { AdvanceInvincibilityCounter(); }
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!base.TakeSwordHit(sourcePosition, damage)) return false;
        _hitPending = true;
        return true;
    }

    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && !Replaced &&
        !DiedInHazard && State == 3 && SwitchHookSubstate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        // collisionEffect2e uses Link's position for the reserved weapon slot.
        _stunCounter = KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), linkPosition.Floor()) ^ 0x10;
        State = 3;
        SwitchHookSubstate = 0;
    }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    {
        Position = position.Floor() + Position - Position.Floor();
        ZFixed = (zHigh << 8) | (ZFixed & 0xff);
        QueueRedraw();
    }
    public void SwapSwitchHook() => SwitchHookSubstate = 2;
    public void ReleaseSwitchHook()
    {
        if (SwitchHookHeld) SwitchHookSubstate = 3;
    }

    internal bool BeginEmberHit()
    {
        if (IsDead || Replaced || !CollisionEnabled || InvincibilityCounter != 0) return false;
        // ENEMYDMG_2c writes post-hit health and var2a=$9b. The later flame
        // part holds health at one, allowing even a lethal Ember hit to convert.
        Health = System.Math.Max(0, Health - 2);
        if (Health == 0) _hitCollision = false;
        InvincibilityCounter = -90;
        _stunCounter = 90;
        _emberPending = true;
        return true;
    }
    internal bool BeginPegasusHit()
    {
        if (!CollisionEnabled || InvincibilityCounter != 0) return false;
        InvincibilityCounter = -16; // ENEMYDMG_38: no damage, $f0 stun counter.
        _stunCounter = 240;
        return true;
    }
    internal void BeginScentHit(Vector2 source, int damage)
    {
        Health = System.Math.Max(0, Health - damage);
        if (Health == 0) _hitCollision = false;
        _hitPending = true;
        ApplySwordNoKnockback(source, EnemyKnockbackStrength.Normal);
    }
}
