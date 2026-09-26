using Godot;
using System;

namespace oracleofages;

// ENEMY$7c. Native room eligibility/collision resolution belongs to the adapter;
// this actor owns the represented intro, merge initialization and wall-cloud handlers.
internal sealed partial class SmogCharacter : EnemyCharacter
{
    private readonly SmogFireTimer _fireTimer = new(new SmogFireTimerDatabase());
    private SmogWallMovement? _wall;
    private int _phase;
    private ImportedEnemyDefinition? _record;
    private bool _dying;
    internal int NativeInitialRandom { get; private set; }
    private Func<int, byte>? _roomCollision;
    private OracleObjectPosition _largePosition;
    internal int LargeSubstate { get; private set; }
    internal int Counter1 { get; set; }
    internal int Angle { get; private set; }
    internal void DisableCollision() => _collisionEnabled = false;
    private bool _collisionEnabled = true;
    internal override bool CollisionEnabled => _collisionEnabled && base.CollisionEnabled;
    internal int Speed { get; private set; }
    internal int ContactFlags { get; private set; }
    internal int CollisionMode { get; private set; } = 0x4d;
    internal int WallCoordinateSum => _wall?.WallCoordinateSum ?? 0;
    internal int WallDirection => _wall?.Direction ?? throw new InvalidOperationException("Smog merge requires a wall-cloud direction.");
    internal void SetMergeSubId(int subid)
    {
        if (subid is not (0 or 0x80 or 6)) throw new ArgumentOutOfRangeException(nameof(subid));
        SubId = subid; // Interaction$33 changes this byte without reinitializing state.
    }
    internal void UpdateMergedDeletion(Action<Vector2> puff, Action decrementEnemies)
    {
        if (State != 8 || SubId != 6) throw new NotSupportedException("smog_deleteSelf requires initialized subid$06; state0 boss-room initialization is separate.");
        puff(Position); decrementEnemies(); Finish();
    }
    internal OracleObjectPosition NativePosition => SubId == 4 ? _largePosition : _wall?.Position ?? OracleObjectPosition.FromPixels(Position);
    internal void PublishCollision(int flags) => ContactFlags = flags & 255;
    internal void ApplyLargeSwordCollision(int damage, int collision, Vector2 source)
    {
        if (damage is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(damage));
        // ENEMYDMG_30: byte ADD, zero on no carry, pending collision,
        // invincibility32 and zero recoil. A native damage byte00 has no carry.
        int sum = Health + ((-damage) & 255);
        Health = sum >= 256 ? sum & 255 : 0;
        if (Health == 0) DisableCollision();
        PublishCollision(ObjectCollisionFlags.JustHit | collision);
        InvincibilityCounter = 32;
        KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position, source) ^ ObjectAngle.HalfTurn;
    }
    internal int SubId { get; private set; }
    internal int State { get; private set; }
    internal int Counter2 { get; private set; }
    internal int ProjectileCounter => _fireTimer.Remaining;

    internal void InitializeRoomSentinel(ImportedEnemyDefinition record, int subid, Vector2 position)
    {
        if (record.Id != EnemyId.Smog || subid is not (5 or 6))
            throw new NotSupportedException($"Smog room initialization requires subid$05/$06, got ${subid:x2}.");
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        _record = record; _dying = false;
        SubId = subid; State = 0; Counter2 = 0; Visible = false;
        _wall = null; ContactFlags = 0; CollisionMode = EnemyCollisionMode.Smog;
        _collisionEnabled = true; Speed = 0;
    }

    internal void UpdateRoomSentinel(Func<int> initializeBossRoom, Action<Vector2> puff, Action decrementEnemies)
    {
        if (SubId is not (5 or 6)) throw new InvalidOperationException("Smog room sentinel dispatch requires subid$05/$06.");
        if (State == 0)
        {
            // enemyBoss_initializeRoom's returned A reaches ecom_setSpeedAndState8:
            // scrollMode bit0 returns1; forced entry returns transition direction*8.
            // Neither helper sets visibility. A merged state0 cloud retains its
            // existing hidden state and receives the same room side effects.
            Speed = initializeBossRoom() & 255;
            State = 8; _collisionEnabled = false;
            return;
        }
        if (State != 8) throw new NotSupportedException($"Smog room sentinel state${State:x2} is not represented.");
        if (SubId == 6) UpdateMergedDeletion(puff,decrementEnemies);
        // subid5 state8 is a RET: it retains its enemy count and does no work.
    }

    internal void InitializeIntro(ImportedEnemyDefinition record, int subid, Vector2 position)
    {
        if (record.Id != EnemyId.Smog || subid is not (0 or 1))
            throw new NotSupportedException($"smog_state_uninitialized subid${subid:x2}: only intro forms are represented by InitializeIntro.");
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        _record = record; _dying = false;
        SubId = subid; State = 0; Counter2 = 0; Visible = false; _wall = null; ContactFlags = 0; CollisionMode = EnemyCollisionMode.Smog;
        _collisionEnabled = true; Speed = 0;
    }

    internal void InitializeSmallCloud(ImportedEnemyDefinition record, int subid, int phase,
        Vector2 position, int direction, Func<int, byte> collision)
    {
        if (record.Id != EnemyId.Smog || subid is not (2 or 0x82) || phase is < 0 or > 3)
            throw new NotSupportedException($"smog_state_uninitialized subid${subid:x2}, phase${phase:x2}: small-cloud initialization requires $02/$82 and phase0-3.");
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        _record = record; _dying = false;
        SubId = subid; _phase = phase; State = 0; Counter2 = 0; Visible = false; ContactFlags = 0; CollisionMode = EnemyCollisionMode.Smog;
        _collisionEnabled = true; Speed = 0;
        _wall = new(new SmogWallDatabase(), subid, OracleObjectPosition.FromPixels(position), direction, collision, MovementVelocity);
        _roomCollision = collision;
    }

    internal void InitializeMergedCloud(ImportedEnemyDefinition record, int subid, int phase,
        Vector2 position, int direction, Func<int, byte> collision)
    {
        if (record.Id != EnemyId.Smog || subid is not (3 or 0x83) || phase is < 0 or > 3)
            throw new NotSupportedException($"smog_state_uninitialized merged subid${subid:x2}, phase${phase:x2} is not represented.");
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        _record = record; _dying = false;
        SubId = subid; _phase = phase; State = 0; Counter2 = 5; Visible = false; ContactFlags = 0; CollisionMode = EnemyCollisionMode.Smog;
        _collisionEnabled = false; Speed = 0;
        _wall = new(new SmogWallDatabase(), subid, OracleObjectPosition.FromPixels(position), direction, collision, MovementVelocity);
        _roomCollision = collision; LargeSubstate = 0; Counter1 = 0; Angle = ObjectAngle.Up;
    }

    internal void UpdateMergedInitialization(int enemyCount, Action<int> writeSamePageInteractionCounter2,
        Action<int,int> setTile, Func<int> nextRandom)
    {
        if (State != 0 || SubId is not (3 or 0x83))
            throw new InvalidOperationException("smog.s subid3 initialization requires a pending merged cloud.");
        if (Counter2 != 0) Counter2--;
        _collisionEnabled = false;
        if (Counter2 != 0) return;
        _collisionEnabled = true;
        int animation;
        if (enemyCount == 2)
        {
            _largePosition = _wall!.Position;
            SubId = 4; // Discards the turn-sense high bit, preserving default radius/mode.
            _wall = null;
            setTile(0x11, 0xa3);
            animation = 4;
            Speed = 0x0a;
        }
        else
        {
            // Clean US $0f:$70fd: ld e,$47; ld a,$3c; ld (de),a.
            // Enemy slots and interaction slots share page$d0-$df. This writes
            // the same-page interaction's counter2, not Enemy.counter2 ($87).
            writeSamePageInteractionCounter2(60);
            SetCollisionRadii(6,6);
            CollisionMode = EnemyCollisionMode.ProjectileWithRingMod;
            animation = 2;
            Speed = 0x14;
        }
        SetAnimation(animation);
        _fireTimer.Reset(SubId, _phase, nextRandom);
        State = 8; Visible = true;
        ZIndex = NpcCharacter.BehindLinkZIndex; // objectSetVisiblec2
    }

    internal void UpdateLargeCloud(Vector2 target, Action<Vector2> spawnProjectile, Func<int> nextRandom)
    {
        if (State != 8 || SubId != 4 || _roomCollision is null)
            throw new InvalidOperationException("smog_state8_subid4 requires an initialized large cloud.");
        if (IsDead) return;
        if (ContactFlags == 0xa0) { LargeSubstate = 3; Counter2 = 70; }
        ContactFlags &= 0x7f;
        Animation.Advance();
        if (LargeSubstate == 3)
        {
            if (Counter2 != 0) Counter2--;
            if (Counter2 == 0) { LargeSubstate = 0; _collisionEnabled = true; }
            return;
        }
        if (LargeSubstate == 0)
        {
            LargeSubstate = 1; Speed = 0;
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target.Floor());
            Counter1 = 20;
        }
        if (LargeSubstate is not (1 or 2))
            throw new NotSupportedException($"smog.s large substate${LargeSubstate:x2} is not represented.");
        int parameter = Animation.CurrentParameter;
        if (parameter == 1)
        {
            SetAnimation(4);
            _fireTimer.Reset(SubId, _phase, nextRandom);
            return;
        }
        if (parameter != 0)
        {
            spawnProjectile(new(OracleObjectPosition.HighByte(Position.X), (OracleObjectPosition.HighByte(Position.Y) + 8) & 255));
            return;
        }
        if (_fireTimer.Advance()) SetAnimation(5);
        var velocity = MovementVelocity(Speed, Angle);
        _largePosition = _largePosition.Add(velocity.YFixed, velocity.XFixed);
        Position = _largePosition.PixelPosition;
        // ecom_bounceOffScreenBoundary accepts only raw $ff, with native byte
        // wrapping and sideview probe order even in this overhead room.
        Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
            point => _roomCollision((point.Y & 0xf0) | ((point.X & 255) >> 4)) == 0xff);
        QueueRedraw();
        Counter1 = (byte)(Counter1 - 1); // ecom_decCounter1 is DEC (HL), unlike counter2.
        if (Counter1 != 0) return;
        Counter1 = 20;
        Speed += LargeSubstate == 1 ? 5 : -5;
        if (LargeSubstate == 1 && Speed == 0x1e) LargeSubstate = 2;
        else if (LargeSubstate == 2 && Speed == 0) LargeSubstate = 0;
    }

    internal void UpdateNativeFrame(bool frozen, int frameCounter, Func<int> nextRandom,
        Action dispatchHandler, Action dispatchDeath)
    {
        if (IsDead || frozen && State != 0) return;
        SetGlobalFrameCounter(frameCounter);
        try
        {
            if (State == 0)
            {
                // enemyStandardUpdate reloads these fields and animation0 on
                // EVERY state0 pass, including the five-update merge delay.
                var record = _record ?? throw new InvalidOperationException("Smog native update requires imported properties.");
                Health = record.Health;
                SetCollisionRadii(record.RadiusX,record.RadiusY);
                CollisionMode = EnemyCollisionMode.Smog; _collisionEnabled = true;
                RestartAnimation(0);
                NativeInitialRandom = nextRandom() & 255; // Enemy.var3d; var3e = 1.
                dispatchHandler();
            }
            else if ((ContactFlags & ObjectCollisionFlags.JustHit) != 0) dispatchHandler();
            else if ((KnockbackCounter & 0x7f) != 0)
            {
                KnockbackCounter--;
                // Smog's entry dispatches status5 through its normal handler,
                // unlike Smasher. There is no separate recoil movement here.
                dispatchHandler();
            }
            else if (Health == 0) dispatchDeath();
            else dispatchHandler();
        }
        finally
        {
            // _updateEnemiesIfStateIsZero restores palette but skips this tail.
            if (!frozen) { ContactFlags &= 0x7f; AdvanceInvincibilityCounter(); }
            QueueRedraw();
        }
    }

    internal void UpdateBossDeath(Func<Vector2,bool> createExplosion, Action disableLinkCollisionsAndMenu,
        Action markKilledInRoom, Action restoreRoomMusic, Action<int> sound)
    {
        if (IsDead) return;
        if (Health != 0 || (ContactFlags & ObjectCollisionFlags.JustHit) != 0 || (KnockbackCounter & 0x7f) != 0)
            throw new InvalidOperationException("enemyBoss_dead for Smog requires native NO_HEALTH dispatch.");
        if (!_dying)
        {
            _dying = true; _collisionEnabled = false;
            Counter1 = 120; // commonBossCode.s:enemyBoss_dead
            disableLinkCollisionsAndMenu();
            sound(SoundId.SndBossDead);
        }
        if (Counter1 != 0) Counter1--;
        if (Counter1 != 0) { Visible = !Visible; return; }
        Counter1 = 1; // Allocation failure retries next update without flicker.
        if (!createExplosion(Position)) return;
        markKilledInRoom();
        restoreRoomMusic(); // ($7c-$08) >= $68.
        Finish(); // PART$04 retains the enemy count until its own completion.
    }

    internal void UpdateMediumCloud(int roomFlags, Action<Vector2> spawnProjectile,
        Action<Vector2> puff, Action decrementEnemies, Func<int> nextRandom)
    {
        if (State != 8 || _wall is null || SubId is not (3 or 0x83))
            throw new InvalidOperationException("smog_state8_subid3 requires an initialized medium cloud.");
        if (!IsDead) UpdateWallCloud(roomFlags, spawnProjectile, puff, decrementEnemies, nextRandom);
    }

    internal void UpdateSmallCloud(int roomFlags, Action beginBoss, Action<Vector2> spawnProjectile,
        Action<Vector2> puff, Action decrementEnemies, Func<int> nextRandom)
    {
        if (_wall is null || SubId is not (2 or 0x82))
            throw new InvalidOperationException("smog_state8_subid2 requires a small-cloud actor.");
        if (IsDead) return;
        if (State == 0)
        {
            beginBoss();
            SetCollisionRadii(4,4);
            CollisionMode = EnemyCollisionMode.ProjectileWithRingMod; // ENEMYCOLLISION_PROJECTILE_WITH_RING_MOD
            SetAnimation(0);
            _fireTimer.Reset(SubId, _phase, nextRandom);
            Speed = 0x23;
            State = 8; Visible = true; ZIndex = NpcCharacter.BehindLinkZIndex;
            return;
        }
        if (State != 8) throw new NotSupportedException($"smog.s small-cloud state${State:x2} is not represented.");
        UpdateWallCloud(roomFlags, spawnProjectile, puff, decrementEnemies, nextRandom);
    }

    private void UpdateWallCloud(int roomFlags, Action<Vector2> spawnProjectile,
        Action<Vector2> puff, Action decrementEnemies, Func<int> nextRandom)
    {
        int collision = ContactFlags & ObjectCollisionFlags.TypeMask;
        if ((ContactFlags & ObjectCollisionFlags.JustHit) != 0 && collision is >= 4 and <= 9) Counter2 = 30;
        // The native enemy pass clears bit7 after dispatch. This handler only
        // reads it above; retain the low collision ID for subsequent updates.
        ContactFlags &= 0x7f;
        if (Counter2 != 0) Counter2--;
        if (Counter2 != 0) { Animation.Advance(); return; }
        if ((roomFlags & 0x40) != 0)
        {
            puff(Position); decrementEnemies(); Finish(); return;
        }
        Animation.Advance();
        if (Animation.CurrentParameter != 0)
        {
            SetAnimation((SubId & 15) * 2 - 4);
            _fireTimer.Reset(SubId, _phase, nextRandom);
            spawnProjectile(Position); // Source copies position before movement.
        }
        if (_fireTimer.Advance()) SetAnimation((SubId & 15) * 2 - 3);
        _wall!.Update(puff);
        Position = _wall.Position.PixelPosition;
        QueueRedraw();
    }

    // The native room owner controls update eligibility and allocation order.
    // spawn must preserve source unchecked-allocation diagnostics when full.
    internal void UpdateIntro(bool textActive, Action<int> showText,
        Action<Vector2, int> spawn, Action<Vector2> puff, Action decrementEnemies, Func<int> nextRandom)
    {
        if (IsDead) return;
        if (State == 0)
        {
            if (SubId == 0) showText(0x2f26);
            Counter2 = SubId == 0 ? 60 : 120;
            SetAnimation(SubId == 0 ? 4 : 0);
            _fireTimer.Reset(SubId, 0, nextRandom);
            State = 8; Visible = true; ZIndex = NpcCharacter.BehindLinkZIndex;
            return; // Initialization does not fall through into the countdown.
        }
        if (State != 8) throw new NotSupportedException($"smog.s intro state${State:x2} is not represented.");
        if (SubId == 0 && textActive) return; // retIfTextIsActive, before animation.
        Animation.Advance();
        if (Counter2 != 0) Counter2--;
        if (Counter2 != 0) return;
        if (SubId == 0)
        {
            // objectCopyPositionWithOffset writes high bytes, retaining native
            // byte wrapping. Left child is allocated before the right child.
            int y = OracleObjectPosition.HighByte(Position.Y), x = OracleObjectPosition.HighByte(Position.X);
            spawn(new((x - 16) & 255, y), 1);
            spawn(new((x + 16) & 255, y), 1);
        }
        puff(Position);
        decrementEnemies();
        Finish();
    }
}
