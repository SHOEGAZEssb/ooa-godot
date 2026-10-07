using Godot;
using System;

namespace oracleofages;

// common/enemies/wizzrobe.s: three profiles share status and reservation ownership.
internal sealed partial class WizzrobeCharacter : EnemyCharacter, ISwitchHookEnemy, ITerrainShadowSource
{
    private readonly WizzrobeBehaviorProfile _data = EnemyBehaviorTables.Shared.Wizzrobe;
    private OracleRandom _random = null!;
    private OracleRuntimeState _memory = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private Func<Vector2> _camera = null!;
    private int _slot = -1, _stunCounter, _speedZ;
    private bool _enabled, _linkHit;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter1 { get; set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; set; }
    internal int Reservation { get; private set; }
    internal Vector2 TargetPosition { get; private set; }
    internal int SwitchHookSubstate { get; private set; }
    internal int StunCounter => _stunCounter;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _enabled && base.CollisionEnabled;
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void BindEnemySlot(int slot) => _slot = slot;
    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position,
        OracleRandom random, OracleRuntimeState memory, Func<Vector2> camera)
    {
        Record = record; _room = room; _random = random; _memory = memory; _camera = camera;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        _movement = new(this, room);
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, checksHazards: true,
            knockbackHoleAnimation: false, nativeSpeed: () => Speed);
        ConfigureHazards(room, animateWhileFallingInHole: false, zPosition: () => ZFixed);
        Visible = false;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
    }
    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate's var3d, before species RNG.
        if (Record.SubId == 0) { State = 8; Counter1 = _data.GreenInitial; }
        else if (Record.SubId == 1)
        {
            State = 8;
            // Each newly initialized red clears this shared union in native slot order.
            for (int i = 0; i < _data.ReservationBytes; i++)
                _memory.SetWramByte(WramAddress.wWizzrobePositionReservations + i, 0);
        }
        else if (Record.SubId == 2)
        {
            State = 11; Speed = _data.BlueSpeed; Counter1 = _data.BlueWait;
            Angle = _random.Next().Value & ObjectAngle.CardinalMask;
            SetAnimationFromAngle();
        }
        else throw new NotSupportedException($"wizzrobe.s: unsupported ENEMY$40:${Record.SubId:x2}.");
    }
    internal bool UpdateFrame(Vector2 target, int frame, out Vector2 projectilePosition)
    {
        // Blue state8 calls ecom_spawnProjectile before applying its own speed.
        projectilePosition = Position.Floor();
        bool hit = NativeHitPending;
        bool stunned = State != 0 && !hit && !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position, State, frame, ref _stunCounter, ref z, ref _speedZ);
            ZFixed = z;
        }
        if (!DiedInHazard && CheckHazards()) { AdvanceInvincibilityCounter(); return false; }
        if (hit && Record.SubId == 1 && _stunCounter == 0 && !_linkHit) RemoveReservation();
        if (BeginFrame() || stunned) { _linkHit = false; return false; }
        if (Health == 0) { Finish(); return false; }
        if (State == 0) { PrepareForScreenTransition(); return false; }
        if (State == 3) { UpdateSwitchHook(); return false; }
        bool fire = Record.SubId switch
        {
            0 => UpdateGreen(target),
            1 => UpdateRed(target),
            2 => UpdateBlue(target),
            _ => throw new NotSupportedException($"wizzrobe.s: unsupported ENEMY$40:${Record.SubId:x2}.")
        };
        QueueRedraw();
        return fire;
    }
    private void DecCounter() => Counter1 = (Counter1 - 1) & 0xff;
    private void Flicker() => Visible = !Visible;
    private void Aim(Vector2 target)
    {
        Angle = (OracleObjectMovement.Shared.RelativeAngle(Position, target) + 4) & ObjectAngle.CardinalMask;
        SetAnimationFromAngle();
    }
    private void SetAnimationFromAngle() => RestartAnimation((((Angle + 4) & ObjectAngle.CardinalMask) >> 3) + 1);
    private bool UpdateGreen(Vector2 target)
    {
        switch (State)
        {
            case 8:
                DecCounter();
                if (Counter1 == 0) { State = 9; Counter1 = _data.GreenPhaseIn; Visible = true; }
                break;
            case 9:
                DecCounter();
                if (Counter1 == 0) { State = 10; Counter1 = _data.AttackCounter; _enabled = true; Aim(target); }
                else if (Counter1 >= _data.GreenFlickerThreshold) Flicker();
                break;
            case 10:
                DecCounter();
                if (Counter1 == 0) { State = 11; _enabled = false; RestartAnimation(0); }
                else return Counter1 == _data.FireCounter;
                break;
            case 11:
                Counter1 = (Counter1 + 1) & 0xff;
                if (Counter1 >= _data.GreenPhaseOut) { State = 8; Counter1 = _data.GreenWait; Visible = false; }
                else if (Counter1 >= _data.GreenFlickerThreshold) Flicker();
                break;
            default: throw InvalidState();
        }
        return false;
    }
    private bool UpdateRed(Vector2 target)
    {
        switch (State)
        {
            case 8:
                if (!ChooseSpawnPosition(out var position) || !ReservePosition(position)) break;
                Position = position + Position - Position.Floor();
                State = 9; Counter1 = _data.RedPhaseIn; Aim(target);
                break;
            case 9:
                DecCounter();
                if (Counter1 == 0) { State = 10; Counter1 = _data.AttackCounter; _enabled = true; Visible = true; }
                else Flicker();
                break;
            case 10:
                DecCounter();
                if (Counter1 == 0) { State = 11; Counter1 = _data.RedPhaseOut; _enabled = false; }
                else return Counter1 == _data.FireCounter;
                break;
            case 11:
                DecCounter();
                if (Counter1 == 0) { State = 8; RemoveReservation(); }
                else if (Counter1 == _data.RedHideCounter) Visible = false;
                else if (Counter1 > _data.RedHideCounter) Flicker();
                break;
            default: throw InvalidState();
        }
        return false;
    }
    private bool UpdateBlue(Vector2 target)
    {
        switch (State)
        {
            case 8:
                DecCounter();
                if (Counter1 == 0) { State = 9; _enabled = false; return false; }
                Counter2 = (Counter2 - 1) & 0xff;
                if (Counter2 == 0) { Aim(target); Counter2 = (_random.Next().Value & _data.BlueReaimMask) + _data.BlueReaimBase; }
                bool fire = (Counter1 & _data.BlueFireMask) == 0;
                if (!_movement.MoveUsingAdjacentWalls(Angle, Speed, allowHoles: false, topDown: false))
                { State = 9; _enabled = false; }
                return fire; // Firing precedes the wall response on this same update.
            case 9:
                if (!ChooseSpawnPosition(out var position)) { Flicker(); break; }
                TargetPosition = position;
                State = 10; ZFixed = (short)(ZFixed - 256);
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, TargetPosition); SetAnimationFromAngle();
                break;
            case 10:
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, TargetPosition);
                Flicker();
                if (((((int)TargetPosition.X - OracleObjectPosition.HighByte(Position.X)) + _data.TargetRadius) & 0xff) < _data.TargetDiameter &&
                    ((((int)TargetPosition.Y - OracleObjectPosition.HighByte(Position.Y)) + _data.TargetRadius) & 0xff) < _data.TargetDiameter)
                {
                    State = 11; Counter1 = _data.BlueWait; ZFixed &= 0xff; Aim(target); Visible = true;
                }
                else
                {
                    var velocity = MovementVelocity(Speed, Angle);
                    Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
                }
                break;
            case 11:
                DecCounter();
                if (Counter1 != 0) { Flicker(); break; }
                State = 8; _enabled = true;
                var roll = _random.Next();
                Counter1 = (roll.High & _data.BlueAttackMask) + _data.BlueAttackBase;
                Counter2 = (roll.Low & _data.BlueAimMask) + _data.BlueAimBase;
                Aim(target); Visible = true;
                break;
            default: throw InvalidState();
        }
        return false;
    }
    private bool ChooseSpawnPosition(out Vector2 position)
    {
        Vector2 camera = _camera();
        int y = (((OracleObjectPosition.HighByte(camera.Y) + (_random.Next().Value & _data.SpawnYMask)) & _data.SpawnTileMask) + _data.SpawnCenter) & 0xff;
        int x;
        do { x = _random.Next().Value & _data.SpawnXMask; } while (x >= _data.SpawnXLimit);
        x = (((OracleObjectPosition.HighByte(camera.X) + x) & _data.SpawnTileMask) + _data.SpawnCenter) & 0xff;
        position = new(x, y);
        return _room.GetTerrainInfo(position).Collision == 0;
    }
    private bool ReservePosition(Vector2 position)
    {
        if (_slot < 0) throw new InvalidOperationException("ENEMY$40:$01 needs its native slot to reserve a destination.");
        byte packed = (byte)_room.GetPackedPosition(position);
        for (int i = 0; i < _data.ReservationCount; i++)
            if (_memory.ReadWramByte(WramAddress.wWizzrobePositionReservations + 2 * i) == packed) return false;
        for (int i = 0; i < _data.ReservationCount; i++)
        {
            int address = WramAddress.wWizzrobePositionReservations + 2 * i;
            if (_memory.ReadWramByte(address) != 0) continue;
            _memory.SetWramByte(address, packed);
            _memory.SetWramByte(address + 1, (byte)(0xd0 + _slot));
            Reservation = (address + 1) & 0xff;
            return true;
        }
        return false;
    }
    internal void RemoveReservation()
    {
        int address = (WramAddress.wWizzrobePositionReservations & 0xff00) | Reservation;
        if (_slot < 0 || _memory.ReadWramByte(address) != 0xd0 + _slot) return;
        _memory.SetWramByte(address, 0); _memory.SetWramByte(address - 1, 0);
    }
    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!TakeDeferredNoKnockbackHit(origin, damage)) return false;
        _linkHit = false; DeferNativeHitStatus(); return true;
    }
    internal void MarkLinkHit() { _linkHit = true; DeferNativeHitStatus(); }
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;
    internal void BeginSeedStatus(bool ember, int damage)
    {
        var status = ember ? _data.EmberStatus : _data.PegasusStatus;
        if (ember)
        {
            Health = Math.Max(0, Health - damage);
            if (Health == 0) _enabled = false;
            _linkHit = false; DeferNativeHitStatus(); KnockbackCounter = 0;
        }
        InvincibilityCounter = unchecked((sbyte)status[1].Value);
        _stunCounter = status[3].Value;
    }
    internal void EnterGaleState()
    {
        State = 5; _stunCounter = 0;
    }
    private InvalidOperationException InvalidState() => new($"wizzrobe.s: ENEMY$40:${Record.SubId:x2} unsupported state${State:x2}.");
    private void UpdateSwitchHook()
    {
        if (SwitchHookSubstate == 0) { SwitchHookSubstate = 1; return; }
        if (SwitchHookSubstate != 3) return;
        int z = ZFixed;
        if (OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, _data.HookGravity))
        {
            State = _data.HookRecovery[Record.SubId * 2].Value;
            Counter1 = _data.HookRecovery[Record.SubId * 2 + 1].Value;
            _enabled = false;
        }
        ZFixed = z;
    }
    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && !DiedInHazard && State == 3 && SwitchHookSubstate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        _stunCounter = KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), linkPosition.Floor()) ^ ObjectAngle.HalfTurn;
        State = 3; SwitchHookSubstate = 0; _enabled = false;
        // collisionEffect2e writes state3 directly without JUST_HIT. A red
        // retains its reservation until its later phase-out counter expires.
        _linkHit = false;
    }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    { Position = position.Floor() + Position - Position.Floor(); ZFixed = (zHigh << 8) | (ZFixed & 0xff); QueueRedraw(); }
    public void SwapSwitchHook() => SwitchHookSubstate = 2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) SwitchHookSubstate = 3; }
}
