using Godot;
using System;

namespace oracleofages;

internal partial class LikeLikeCharacter : EnemyCharacter, ISwitchHookEnemy
{
    private readonly LikeLikeBehaviorProfile _data = EnemyBehaviorTables.Shared.LikeLike;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _enabled;
    private bool _hitPending;
    private bool _capturePending;
    private bool _propertiesLoaded;
    private int _stun;
    private int _speedZ;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; set; }
    internal int StunCounter => _stun;
    internal override void ApplyBoomerangStun(int updates) => _stun = updates;
    internal bool PendingHit => _hitPending || _capturePending;
    internal override bool CollisionEnabled => base.CollisionEnabled && _enabled && State != 3;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    internal int SwitchHookSubstate { get; private set; }

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position, OracleRandom random)
    {
        if (record.Id != 0x24 || record.SubId != 0)
            throw new NotSupportedException($"likelike.s: ordinary handler requires $24:$00, got ${record.Id:x2}:${record.SubId:x2}.");
        Record = record; _room = room; _random = random;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        _movement = new(this, room);
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, checksHazards: true,
            nativeSpeed: () => _data.SpeedRaw);
        ConfigureHazards(room, zPosition: () => ZFixed);
        Visible = false;
    }

    internal void InitializeState()
    {
        if (State != 0) return;
        InitializeCommonProperties();
        State = 8;
        Visible = true;
        ZIndex = NpcCharacter.BehindLinkZIndex;
    }

    private void InitializeCommonProperties()
    {
        if (_propertiesLoaded) return;
        _random.Next(); // enemyStandardUpdate.var3d before enemyCode24, including hazards.
        _propertiesLoaded = true;
    }

    internal void MarkCapture() => _capturePending = true;
    internal void ClearSeedStun() => _stun = 0;

    internal void UpdateFrame(Player player, bool buttonPressed, int frameCounter, Action shieldLost)
    {
        if (IsDead) return;
        try
        {
            if (State == 0) InitializeCommonProperties();
            bool stunned = State != 0 && !PendingHit && !HasActiveKnockback && Health > 0 && _stun != 0;
            if (stunned)
            {
                int z = ZFixed;
                Position = EnemyStunMotion.Update(Position, State, frameCounter, ref _stun, ref z, ref _speedZ);
                ZFixed = z;
            }
            // The original deliberately checks GLOBAL Link state, not an
            // owner pointer. This precedes common hazard/status dispatch.
            if (player.EnemyGrabActive && (ZFixed >> 8) >= 0 &&
                _room.GetTerrainInfo(OracleObjectMath.ToPixelPosition(Position) + new Vector2(0,5)).Hazard != HazardType.None)
                ReleaseLink(player);
            if (ContinueHazard() || CheckHazards()) return;
            if (State == 0) { InitializeState(); return; }
            if (_capturePending)
            {
                _capturePending = false;
                if (LinkWallProbe.Shared.SurroundedByWalls(Position, (_room.TilesetFlags & 0x20) != 0, _room.IsSolid))
                { ReleaseLink(player); return; }
                State = 11; Counter1 = 0; Counter2 = _data.HoldFrames;
                _enabled = false;
                player.CopyLikeLikePosition(Position);
                RestartAnimation(1);
                ZIndex = NpcCharacter.InFrontOfLinkZIndex;
                return;
            }
            if (_hitPending) { _hitPending = false; return; }
            if (HasActiveKnockback) { BeginFrame(advanceInvincibility: false); return; }
            if (Health == 0) { Finish(); return; }
            if (stunned) return;
            switch (State)
            {
                case 3:
                    if (SwitchHookSubstate == 0) SwitchHookSubstate = 1;
                    else if (SwitchHookSubstate == 3)
                    {
                        int z = ZFixed;
                        if (OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, 0x20)) State = 9;
                        ZFixed = z;
                    }
                    return;
                case 8:
                    _enabled = true;
                    State = 9;
                    goto case 9;
                case 9:
                    var roll = _random.Next();
                    Angle = roll.High & _data.AngleMask;
                    Counter1 = _data.DurationBase + (roll.Low & _data.DurationMask);
                    State = 10;
                    break;
                case 10:
                    Counter1 = (Counter1 - 1) & 0xff;
                    if (Counter1 == 0 || !_movement.MoveAtAngle(Angle, _data.SpeedRaw, allowHoles: false)) State = 9;
                    break;
                case 11:
                    if (--Counter2 != 0)
                    {
                        if (buttonPressed) Counter1 = (Counter1 + 1) & 0xff;
                        break;
                    }
                    Counter2 = _data.CooldownFrames; State = 12;
                    if (Counter1 < _data.ShieldEscapePresses && player.Inventory.HasTreasure(TreasureDatabase.TreasureShield))
                    {
                        player.Inventory.LoseTreasure(TreasureDatabase.TreasureShield);
                        shieldLost();
                    }
                    Angle = _random.Next().Value & _data.AngleMask;
                    ZIndex = NpcCharacter.BehindLinkZIndex;
                    ReleaseLink(player);
                    return;
                case 12:
                    if (--Counter2 == 0) { State = 9; _enabled = true; }
                    else if (!_movement.MoveAtAngle(Angle, _data.SpeedRaw, allowHoles: false))
                        Angle = _random.Next().Value & _data.AngleMask;
                    break;
                default: throw new NotSupportedException($"likelike.s:$24:$00 state ${State:x2} is not represented.");
            }
            AdvanceAnimation();
        }
        finally { AdvanceInvincibilityCounter(); QueueRedraw(); }
    }

    private void ReleaseLink(Player player)
    { player.ReleaseLikeLikeGrab(); RestartAnimation(0); }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!CollisionEnabled || InvincibilityCounter != 0 || PendingHit) return false;
        Health = Math.Max(0, Health - Math.Max(1, damage));
        if (Health == 0) _enabled = false;
        InvincibilityCounter = SwordInvincibilityFrames;
        _hitPending = true;
        return true;
    }

    internal void BeginEmberHit()
    {
        Health = Math.Max(0, Health - 2);
        if (Health == 0) _enabled = false;
        _hitPending = true;
        InvincibilityCounter = -90;
        _stun = 90;
    }
    internal void BeginPegasusHit()
    {
        // ENEMYDMG_38=$29 has no bit6: it does not publish JUST_HIT.
        InvincibilityCounter = -16; _stun = 240;
    }

    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && !DiedInHazard && State == 3 && SwitchHookSubstate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        _stun = KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), linkPosition.Floor()) ^ 0x10;
        State = 3; SwitchHookSubstate = 0;
    }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    { Position = position.Floor() + Position - Position.Floor(); ZFixed = (zHigh << 8) | (ZFixed & 0xff); QueueRedraw(); }
    public void SwapSwitchHook() => SwitchHookSubstate = 2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) SwitchHookSubstate = 3; }
}
