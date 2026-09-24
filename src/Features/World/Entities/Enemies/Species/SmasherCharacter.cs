using Godot;
using System;

namespace oracleofages;

// One native ENEMY slot per instance. The room adapter supplies allocation,
// grabbed-state motion and common boss death side effects.
internal sealed partial class SmasherCharacter : EnemyCharacter
{
    private readonly SmasherBehaviorProfile _data = EnemyBehaviorTables.Shared.Smasher;
    private OracleRoomData _room = null!;
    internal OracleRoomData Room => _room;
    private EnemyTerrainMovement _movement = null!;
    private OracleRandom _random = null!;
    private SmasherCharacter _related = null!;
    private Vector2 _pickupTarget;
    private int _z, _speedZ;
    private bool _beganFight;
    private bool _collisionEnabled = true;
    private bool _dying;
    private int _initialHealth;
    private bool _propertiesLoaded;
    internal bool PendingCollision { get; private set; }
    internal int CollisionMode { get; private set; } = 0x45;
    internal int DamageQuarters { get; private set; }
    internal void PublishCollision() => PendingCollision = true;
    internal int NativeSlot { get; private set; } = -1;
    internal int NativeSubId { get; private set; }
    internal void BindNativeSlot(int slot)
    {
        if (State != 0 || slot is < 0 or >= 16)
            throw new InvalidOperationException("ENEMY_SMASHER $74 slot binding requires an uninitialized native slot.");
        NativeSlot = slot;
    }
    internal bool IsBall { get; private set; }
    internal int State { get; private set; }
    internal int Counter1 { get; set; }
    internal int Counter2 { get; private set; }
    internal int ExpirationCounter { get; private set; }
    internal int Angle { get; private set; }
    internal int Direction { get; private set; }
    internal int Speed { get; private set; }
    internal int Palette { get; private set; } = 3;
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    internal int GrabSubstate { get; private set; }
    internal override bool CollisionEnabled => _propertiesLoaded && _collisionEnabled && !IsDead;
    internal override Texture2D CurrentDrawTexture => DrawsDamagePalette ? Animation.DamageTexture : Animation.CurrentTextureForPalette(Palette);
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (_z >> 8);

    internal void InitializePending(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, int nativeSlot)
    {
        if (record.Id != 0x74 || record.SubId is not (0 or 1))
            throw new NotSupportedException("smasher.s requires $74:$00/$01 after native slot normalization.");
        if (nativeSlot is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(nativeSlot));
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true, paletteVariants: [1,2]);
        NativeSlot = nativeSlot; NativeSubId = record.SubId; IsBall = record.SubId == 0;
        _initialHealth = record.Health; _room = room; _movement = new(this, room); _random = random;
        DamageQuarters = record.DamageQuarters;
        Visible = false;
    }

    // Existing isolated motion fixtures enter after allocation. Live actors
    // start with InitializePending and dispatch the native state-zero path.
    internal void InitializeLinked(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, SmasherCharacter related)
    {
        InitializePending(record, room, position, random, record.SubId);
        _related = related; _propertiesLoaded = true;
        EnterInitializedState();
    }

    internal void UpdateInitializationFrame(int frameCounter, Action initializeBossRoom,
        Func<SmasherCharacter?> spawnUncountedParent, bool interactionSlotAvailable = true,
        Func<Vector2, bool>? createInitializationPuff = null)
    {
        if (State != 0) throw new InvalidOperationException("ENEMY_SMASHER $74 initialization requires state $00.");
        // enemyStandardUpdate reloads properties and consumes var3d RNG on
        // every failed allocation, including invisible state-zero retries.
        Health = _initialHealth; _propertiesLoaded = true; _collisionEnabled = true;
        PendingCollision = false; CollisionMode = 0x45;
        Palette = 3; RestartAnimation(0); _random.Next();
        if (NativeSubId == 0)
        {
            if ((frameCounter & 1) == 0 && ++ExpirationCounter >= _data.ExpirationEvenTicks)
            {
                // The timer precedes state dispatch: no further room setup or
                // parent allocation occurs after expiry. State$0d first tries
                // a puff and returns on pool exhaustion before reading the link.
                ExpirationCounter = 0;
                State = 0x0d;
                if (!interactionSlotAvailable) return;
                UpdateBall(createInitializationPuff ?? throw new InvalidOperationException(
                    "Smasher initialization expiry requires the room's puff allocator."), _ => { }, null);
                return;
            }
            initializeBossRoom();
            var child = spawnUncountedParent();
            if (child is null) return;
            if (child.State != 0 || child.NativeSubId != 1 || child.NativeSlot == NativeSlot)
                throw new InvalidOperationException("smasher.s $74 parent allocation requires a distinct uninitialized $01 slot.");
            _related = child; child._related = this;
            child.CopyPositionHigh(this);
            if (child.NativeSlot < NativeSlot)
            {
                child.NativeSubId = 0x80; child.IsBall = true;
                NativeSubId = 1; IsBall = false;
            }
        }
        else if (_related is null)
            throw new InvalidOperationException("smasher.s $74 child initialization requires relatedObj1.");
        if (NativeSubId == 0x80) NativeSubId = 0;
        EnterInitializedState();
    }

    private void EnterInitializedState()
    {
        State = 8;
        // enemySetAnimation restores A from hRomBank ($0f for enemyCode74);
        // the parent branch instead reaches this helper after dec subid = 0.
        Speed = IsBall ? 0x0f : 0;
        if (IsBall)
        {
            Position = new((byte)((int)Position.X - _data.InitialBallOffsetX) + Position.X % 1, Position.Y);
            Palette = 1; SetAnimation(4);
        }
        Visible = true;
        ZIndex = NpcCharacter.BehindLinkZIndex; // ecom_setSpeedAndState8AndVisible -> visible$c2.
    }

    internal void UpdateNormalFrame(Vector2 enemyTarget, int frameCounter, Func<Vector2, bool> createPuff,
        Action beginMiniboss, Action<int> sound, Action<int>? setReservedItemAngle = null,
        Action<int>? setLinkGrabState = null, Action? forceDrop = null, Action? handleDeath = null,
        Action? groundBallContact = null)
    {
        if (IsDead) return;
        try
        {
            bool justHit = PendingCollision;
            PendingCollision = false;
            // enemyStandardUpdate prioritizes and decrements knockback before
            // checking health. Smasher's handler performs no Z/AI/timer update.
            if (!justHit && KnockbackCounter != 0)
            {
                KnockbackCounter--;
                if (!_movement.MoveAtAngle(KnockbackAngle, 0x50, allowHoles: true, nativeSpeed: Speed)) KnockbackCounter = 0; // SPEED_200
                return;
            }
            if (!justHit && Health == 0)
            {
                (handleDeath ?? throw new InvalidOperationException("ENEMY_SMASHER $74 requires its native death owner."))();
                return;
            }
            if (State == 0)
                throw new NotSupportedException($"smasher.s $74 state ${State:x2}: dispatch requires native initialization/death owner.");
            if (IsBall && State < 0x0d && (frameCounter & 1) == 0 && ++ExpirationCounter >= _data.ExpirationEvenTicks)
            {
                ExpirationCounter = 0;
                if (State == 2 && GrabSubstate < 2)
                    (forceDrop ?? throw new InvalidOperationException("smasher_ball_makeLinkDrop requires the held-item owner."))();
                State = 0x0d;
            }
            // The common dispatch table maps $01/$03-$07 to RET. Respawn
            // timing runs first and can replace one of these states with $0d.
            if (State < 8 && State != 2) return;
            if (State == 2) UpdateGrabbed(setReservedItemAngle, setLinkGrabState, sound);
            else if (IsBall) UpdateBall(createPuff, sound, groundBallContact);
            else UpdateParent(enemyTarget, beginMiniboss);
        }
        finally { AdvanceInvincibilityCounter(); QueueRedraw(); }
    }

    internal void UpdateDeathFrame(Func<Vector2, bool> createPuff, Func<Vector2, bool> createExplosion,
        Action disableLinkCollisionsAndMenu, Action markKilledInRoom, Action restoreRoomMusic,
        Action<int> sound, Action forceDrop)
    {
        if (IsDead) return;
        if (Health != 0 || KnockbackCounter != 0)
            throw new InvalidOperationException("ENEMY_SMASHER $74 death dispatch requires NO_HEALTH after knockback.");
        if (IsBall)
        {
            if (State == 2 && GrabSubstate < 2) forceDrop();
            createPuff(Position); // Ball deletion is unconditional even when no puff slot exists.
            Finish();
            return;
        }
        if (!_dying)
        {
            // ecom_killRelatedObj1 only writes health/collision: the lower ball
            // slot dispatches death on the following update, not recursively.
            _related.Health = 0;
            _related._collisionEnabled = false;
            _collisionEnabled = false;
            _dying = true;
            Counter1 = _data.DeathFrames;
            disableLinkCollisionsAndMenu();
            sound(OracleSoundEngine.SndBossDead);
        }
        if (--Counter1 != 0)
        {
            Visible = !Visible; // ecom_flickerVisibility toggles bit 7.
            return;
        }
        Counter1 = 1; // Retry allocation every update without toggling visibility.
        if (!createExplosion(Position)) return;
        markKilledInRoom();
        restoreRoomMusic();
        Finish();
    }

    internal void BeginGrab()
    {
        if (!IsBall || State is not (9 or 10)) throw new InvalidOperationException("$74 ball is not in a grabbable ground/pickup state.");
        State = 2; GrabSubstate = 0;
    }
    internal void CopyCarriedPosition(Vector2 position, int zHigh)
    { Position = position.Floor() + Position - Position.Floor(); SetZHigh(zHigh); }
    internal void ReleaseGrab(int angle)
    { State = 2; GrabSubstate = 2; Angle = angle; }
    internal void FinishGrabBounce() => GrabSubstate = 3;
    internal void DropGrab()
    {
        if (State == 2 && GrabSubstate < 2) { GrabSubstate = 3; Angle = 0xff; }
    }

    private void UpdateGrabbed(Action<int>? setReservedItemAngle, Action<int>? setLinkGrabState, Action<int> sound)
    {
        if (!IsBall) throw new InvalidOperationException("Only Smasher's $74:$00 ball may enter grabbed state.");
        switch (GrabSubstate)
        {
            case 0:
                GrabSubstate = 1;
                (setLinkGrabState ?? throw new InvalidOperationException("$74 grab requires wLinkGrabState2 owner."))(0x20);
                ZIndex = NpcCharacter.InFrontOfLinkZIndex; return;
            case 1: return;
            case 2:
                var writeAngle = setReservedItemAngle ?? throw new InvalidOperationException("$74 release requires reserved item C angle owner.");
                bool Wall(Vector2 point) => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height ||
                    _room.IsSolidForEnemyMovement(point, holesAreWalls: true);
                int reflected = Angle == 0xff ? _data.BounceDroppedBall(Position, Wall) :
                    EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle, point => Wall(point));
                if (reflected != Angle) { Angle = reflected; writeAngle(Angle); }
                if ((_related?.InvincibilityCounter ?? _data.UnlinkedInvincibility) != 0 ||
                    (byte)((_z >> 8) - (_related is null ? _data.UnlinkedZ : _related._z >> 8) + _data.HitZBias) >= _data.HitZSpan ||
                    !RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, _related?.CollisionBounds ?? _data.UnlinkedBounds)) return;
                int knockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position, _related?.Position ?? _data.UnlinkedPosition);
                if (_related is not null)
                {
                    _related.InvincibilityCounter = _data.HitInvincibility;
                    _related.KnockbackCounter = _data.HitKnockback;
                    _related.Health = (byte)(_related.Health - 1);
                    _related.KnockbackAngle = knockbackAngle;
                }
                // Null-related writes target mapper addresses $00ab/$00ad/
                // $00a9/$00ac, leaving ROM and stored SRAM bytes unchanged.
                // They cannot establish invincibility for the next release update.
                // The native handler changes the controlling ITEM's angle;
                // it does not overwrite the ball's Enemy.angle here.
                writeAngle(knockbackAngle ^ 0x10);
                sound(OracleSoundEngine.SndBossDamage); return;
            case 3: State = 8; ZIndex = NpcCharacter.BehindLinkZIndex; return;
            default: throw new NotSupportedException($"smasher_state_grabbed substate ${GrabSubstate:x2} is not represented.");
        }
    }

    private void UpdateParent(Vector2 target, Action beginMiniboss)
    {
        switch (State)
        {
            case 8:
                State = 9; Counter1 = 1; Speed = 0x1e; // SPEED_c0
                if (!_beganFight) { _beganFight = true; beginMiniboss(); }
                goto case 9;
            case 9:
                if (_related.State == 9)
                {
                    int ballX = (int)_related.Position.X;
                    int offset = (int)Position.X >= ballX ? _data.PickupOffsetX : -_data.PickupOffsetX;
                    int x = (byte)(ballX + offset);
                    if ((byte)(x - _data.PickupMinimumX) >= _data.PickupSpanX) x = (byte)(x - 2 * offset);
                    _pickupTarget = new(x, (int)_related.Position.Y);
                    State = 10; Speed = 0x28; // SPEED_100
                    SetAnimation(FaceTarget(_pickupTarget)); return;
                }
                Counter1 = (byte)(Counter1 - 1);
                if (Counter1 == 0) { Counter1 = _data.WanderFrames; Angle = _data.WanderAngle(_random.Next().Value); UpdateDirection(); }
                _movement.MoveAtAngle(Angle, Speed, allowHoles: false);
                if (Fall()) { _speedZ = _data.HopSpeedZ; SetAnimation(Direction + 1); }
                else if (_speedZ == 0) SetAnimation(Direction);
                return;
            case 10:
                if (_related.State != 9) { BallUnavailable(target); return; }
                if ((byte)((int)Position.X - (int)_pickupTarget.X + _data.ArrivalBias) < _data.ArrivalSpan &&
                    (byte)((int)Position.Y - (int)_pickupTarget.Y + _data.ArrivalBias) < _data.ArrivalSpan)
                {
                    State = 11; SetZHigh(0); Palette = 2; _related.State = 10;
                    SetAnimation(FaceTarget(_related.Position) + 1); return;
                }
                int animation = FaceTarget(_pickupTarget); Move(); SetAnimation(animation);
                if (Fall()) _speedZ = _data.HopSpeedZ;
                return;
            case 11:
                if (_related.State == 2) { BallUnavailable(target); return; }
                if (_related.State < 11) return;
                Palette = 3; State = 12; Counter2 = _data.CarryFrames; Speed = 0x0a; // SPEED_40
                _speedZ = _data.HopSpeedZ; return;
            case 12:
                SetAnimation(FaceTarget(target) + 1);
                if (Counter2 != 0) Counter2--;
                if (!Fall())
                {
                    _movement.MoveAtAngle(Angle, Speed, allowHoles: false);
                    _related.CopyPositionHigh(this); _related.SetZHigh((_z >> 8) + _data.CarriedZOffset);
                }
                else if (Counter2 != 0) _speedZ = _data.HopSpeedZ;
                else { _speedZ = _data.ThrowJumpSpeedZ; State = 13; Palette = 2; }
                return;
            case 13:
                if (Fall()) { State = 8; return; }
                _related.SetZHigh((_z >> 8) + _data.CarriedZOffset);
                if (_speedZ < 0) { SetAnimation(FaceTarget(target) + 1); return; }
                if (_speedZ != 0 || _related.State != 11) return;
                _related.State = 12; _related.Angle = Angle; Palette = 3; SetAnimation(Direction); return;
            default: throw new NotSupportedException($"smasher_parent state ${State:x2} is not represented.");
        }
    }

    private void UpdateBall(Func<Vector2, bool> createPuff, Action<int> sound, Action? groundBallContact)
    {
        switch (State)
        {
            case 8: State = 9; CollisionMode = 0x63; Speed = 0x19; goto case 9; // SPEED_a0
            case 9: groundBallContact?.Invoke(); return;
            case 10:
                if ((_z >> 8) != _data.CarriedZOffset) _z = unchecked((short)(_z - _data.LiftSpeedZ));
                if (Position.Floor() != _related.Position.Floor())
                {
                    // ecom_moveTowardPosition writes angle, not direction.
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position, _related.Position);
                    Move(); return;
                }
                if ((_z >> 8) != _data.CarriedZOffset) return;
                State = 11; _collisionEnabled = true; Speed = 0x78; ZIndex = NpcCharacter.InFrontOfLinkZIndex; return; // SPEED_300
            case 11: return;
            case 12:
                if (Bounce(out bool landed))
                { State = 8; _collisionEnabled = false; ZIndex = NpcCharacter.BehindLinkZIndex; sound(OracleSoundEngine.SndBombLand); return; }
                if (landed) { Speed >>= 1; sound(OracleSoundEngine.SndBombLand); }
                Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
                    point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height ||
                        _room.IsSolidForEnemyMovement(point, holesAreWalls: true));
                Move(); return;
            case 13:
                if (!createPuff(Position)) return;
                // With relatedObj1=0, clean US reads ROM$0004=$30 and writes
                // $0d/$03/$03 to mapper addresses $0004/$009b/$009c. Executed
                // ROM confirms SRAM is disabled, contents unchanged. Save/file
                // operations explicitly enable SRAM; no actor/save bytes change.
                if (_related is not null && _related.State >= 11) { _related.State = 13; _related.Palette = 3; }
                State = 14; _collisionEnabled = false; Counter1 = _data.RespawnFrames; Visible = false; return;
            case 14:
                Counter1 = (byte)(Counter1 - 1);
                if (Counter1 != 0) return;
                State = 15; SetZHigh(_data.RespawnZ); _speedZ = 0;
                var position = _data.RespawnPosition(_random.Next().Value);
                Position = position + Position - Position.Floor();
                createPuff(Position); Visible = true; ZIndex = NpcCharacter.InFrontOfLinkZIndex; return;
            case 15:
                if (Bounce(out bool touched)) { State = 8; ZIndex = NpcCharacter.BehindLinkZIndex; }
                else if (touched) sound(OracleSoundEngine.SndBombLand);
                return;
            default: throw new NotSupportedException($"smasher_ball state ${State:x2} is not represented.");
        }
    }

    private void BallUnavailable(Vector2 target)
    { State = 9; Speed = 0x1e; Counter1 = _data.WanderFrames; Palette = 3; Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target) ^ 0x10; UpdateDirection(); }
    private int FaceTarget(Vector2 target) { Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target); return UpdateDirection(); }
    private int UpdateDirection()
    {
        // Vertical angle returns A=0 without changing Enemy.direction.
        if ((Angle & 15) == 0) return 0;
        return Direction = ((Angle & 16) ^ 16) >> 3;
    }
    private void Move() => Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
    private void SetZHigh(int high) => _z = unchecked((short)((high << 8) | (_z & 255)));
    private void CopyPositionHigh(SmasherCharacter source)
    { Position = source.Position.Floor() + Position - Position.Floor(); SetZHigh(source._z >> 8); }
    private bool Fall() => OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, _data.Gravity);
    private bool Bounce(out bool landed)
    {
        landed = Fall();
        if (!landed) return false;
        int speed = unchecked((short)-_speedZ) >> 1;
        if ((ushort)speed > 0xff80) return true;
        _speedZ = speed;
        return speed == 0;
    }
}
