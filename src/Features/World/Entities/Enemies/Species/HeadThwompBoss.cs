using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_HEAD_THWOMP $79. Bombs select the current even-numbered face; green
/// fires falling shots, blue emits curving shots, purple pounds the room, and
/// red removes one of the boss's four health points.
/// </summary>
internal sealed partial class HeadThwompBoss : EnemyCharacter
{
    private const int ArmoredSwordInvincibilityFrames = 20;
    private static readonly int[,] RotationSpeeds =
    {
        { 0x11, 0x07 },
        { 0x14, 0x08 },
        { 0x17, 0x0a },
        { 0x1a, 0x0b }
    };
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Action<int> _playSound = null!;
    private Action<int> _screenShake = null!;
    private Action _disableLink = null!;
    private Action _restoreRoomMusic = null!;
    private Func<long> _animationTick = null!;
    private HeadThwompState _state = HeadThwompState.WaitingForLink;
    private int _counter = 18;
    private int _counter2;
    private int _direction;
    private int _targetDirection;
    private int _spinDelay;
    private int _projectileCounter;
    private int _phase;
    private int _verticalSpeedFixed;
    private bool _pendingBomb;
    private bool _initialized;
    private bool _deathPending;
    private bool _dying;
    private int _deathCounter;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal HeadThwompState State => _state;
    internal int Direction => _direction;
    internal int Counter => _counter;
    internal bool Defeated => _dying || IsDead;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && !_dying;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        IReadOnlyDictionary<int, Color[]> paletteOverrides,
        Action<int> playSound,
        Action<int> screenShake,
        Action disableLink,
        Action restoreRoomMusic,
        Func<long> animationTick)
    {
        Record = record;
        _room = room;
        _random = random;
        _playSound = playSound;
        _screenShake = screenShake;
        _disableLink = disableLink;
        _restoreRoomMusic = restoreRoomMusic;
        _animationTick = animationTick;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record),
            paletteOverrides: paletteOverrides,
            positionedOam: true);
        Name = "HeadThwomp";
        ZIndex = 10;
        Visible = false;
    }

    internal void UpdateFrame(
        Player player,
        int frameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (!_initialized)
        {
            // enemyBoss_initializeRoom runs as state 0's only update: PALH
            // $81 is already represented by the imported palette override,
            // while this update stops music, installs the collision cells,
            // and makes the enemy visible.
            _initialized = true;
            SetSurroundingSolidity(true);
            Visible = true;
            _playSound(OracleSoundEngine.SndCtrlStopMusic);
            QueueRedraw();
            return;
        }
        if (_deathPending)
        {
            _deathPending = false;
            BeginDeath();
        }
        if (_dying)
        {
            // enemyBoss_dead decrements first and then XORs visible bit 7.
            Visible = !Visible;
            if (--_deathCounter == 0)
            {
                Finish();
                spawns.Add(new BossDeathExplosionSpawn(Position, Record.Id));
                _restoreRoomMusic();
            }
            QueueRedraw();
            return;
        }

        switch (_state)
        {
            case HeadThwompState.WaitingForLink:
                if (player.Position.Y < 0x9c)
                    BeginFight(player);
                break;

            case HeadThwompState.Spinning:
                if (TryBeginPendingBomb())
                    break;
                UpdateBackgroundProjectiles(frameCounter, spawns);
                if (--_counter == 0)
                    RotateNormally();
                break;

            case HeadThwompState.BombPause:
                AdvanceAnimation();
                if (--_counter == 0)
                {
                    _state = HeadThwompState.FastSpin;
                    _counter = 60;
                }
                break;

            case HeadThwompState.FastSpin:
                if (--_counter != 0)
                {
                    RotateBombSpin(animationBase: 8);
                    break;
                }
                _state = HeadThwompState.Decelerating;
                _spinDelay = 1;
                _counter = 1;
                _counter2 = 2;
                // State $0b substate 1 falls straight through to substate 2
                // on the update where its 60-count reaches zero.
                UpdateDeceleratingSpin();
                break;

            case HeadThwompState.Decelerating:
                UpdateDeceleratingSpin();
                break;

            case HeadThwompState.FacePause:
                if (--_counter == 0)
                    SelectFaceState();
                break;

            case HeadThwompState.Green:
                UpdateGreen(spawns);
                break;

            case HeadThwompState.Blue:
                UpdateBlue(spawns);
                break;

            case HeadThwompState.Purple:
                UpdatePurple(player, spawns);
                break;

            case HeadThwompState.Red:
                UpdateRed(spawns);
                break;

            case HeadThwompState.Resume:
                AdvanceAnimation();
                if (--_counter == 0)
                {
                    _state = HeadThwompState.Spinning;
                    _counter = 1;
                    _projectileCounter = 0xf0;
                }
                break;
        }
        QueueRedraw();
    }

    internal bool TryCatchBomb(BombEffect bomb)
    {
        if (_dying ||
            !ChecksForBombsThisUpdate() ||
            Mathf.Abs(bomb.Position.Y - 0x50) > 0x0c ||
            Mathf.Abs(bomb.Position.X - 0x78) > 0x0c ||
            !bomb.ConsumeByBoss())
        {
            return false;
        }

        _pendingBomb = true;
        return true;
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage) =>
        AcceptArmoredSwordHit(ArmoredSwordInvincibilityFrames);

    internal override bool TakeBurnHit(int damage) => false;

    private void BeginFight(Player player)
    {
        // headThwomp_state8 calls setTile with c=$a4 (position) and a=$3d
        // (tile), closing the bottom entrance before background drops begin.
        Vector2 door = PointForPackedPosition(0xa4);
        _room.SetPositionTileAndCollision(
            door, 0x3d, collision: null, _animationTick());
        _playSound(OracleSoundEngine.SndDoorClose);
        player.SetLocalRespawnPosition(new Vector2(0x48, 0x98));
        _state = HeadThwompState.Spinning;
        _counter = 18;
        _projectileCounter = 0xf0;
        _playSound(OracleSoundEngine.MusBoss);
    }

    private bool TryBeginPendingBomb()
    {
        if (!_pendingBomb)
            return false;
        if ((_direction & 1) != 0)
        {
            if (--_counter == 0)
            {
                RotateBombSpin(animationBase: 0);
                BeginBombSpin();
            }
            return true;
        }
        BeginBombSpin();
        return true;
    }

    private void BeginBombSpin()
    {
        _pendingBomb = false;
        _phase = 0;
        _targetDirection = _direction;
        _state = HeadThwompState.BombPause;
        _counter = 6;
        SetAnimation(_direction);
        SetMouthCollision(0x03);
    }

    private void RotateNormally()
    {
        int healthIndex = Mathf.Clamp(Health - 1, 0, 3);
        _direction = (_direction + 1) & 7;
        SetAnimation(_direction);
        // State $09's inline rotation plays SND_CLINK2 on transitional
        // odd-numbered heads. The post-bomb helper below deliberately plays
        // it on the even, fully selected heads instead.
        if ((_direction & 1) != 0)
            _playSound(OracleSoundEngine.SndClink2);
        _counter = RotationSpeeds[
            healthIndex,
            (_direction & 1) == 0 ? 0 : 1];
    }

    private void RotateBombSpin(int animationBase)
    {
        _direction = (_direction + 1) & 7;
        SetAnimation(animationBase + _direction);
        if ((_direction & 1) == 0)
            _playSound(OracleSoundEngine.SndClink2);
    }

    private void UpdateDeceleratingSpin()
    {
        if (--_counter != 0)
            return;

        if (_phase == 0)
        {
            _counter2--;
            if (_counter2 == 0)
            {
                _counter2 = 2;
                _spinDelay++;
                if (_spinDelay >= 0x12)
                {
                    _phase = 1;
                    _counter2 = 6;
                    _counter = 1;
                    // Substate 2 falls through to substate 3 at this exact
                    // boundary instead of waiting for another enemy update.
                    UpdateDeceleratingSpin();
                    return;
                }
            }
            _counter = _spinDelay;
            RotateBombSpin(animationBase: 8);
            return;
        }

        _counter2 += 0x0c;
        _counter = _counter2;
        if (_direction != _targetDirection)
        {
            RotateBombSpin(animationBase: 8);
            return;
        }
        _phase = 0;
        _state = HeadThwompState.FacePause;
        _counter = 0x10;
    }

    private void SelectFaceState()
    {
        _phase = 0;
        switch (_direction >> 1)
        {
            case 0:
                _state = HeadThwompState.Green;
                break;
            case 1:
                _state = HeadThwompState.Blue;
                break;
            case 2:
                _state = HeadThwompState.Purple;
                break;
            case 3:
                _state = HeadThwompState.Red;
                break;
        }
    }

    private void UpdateGreen(ICollection<RoomEntitySpawn> spawns)
    {
        if (_phase == 0)
        {
            _phase = 1;
            _counter = 0xf0;
            SetMouthCollision(0x00);
            return;
        }
        if (TryBeginPendingBomb())
            return;
        if (--_counter == 0)
        {
            BeginResume(1);
            return;
        }
        if (_counter >= 210)
            AdvanceAnimation();
        if ((_counter & 0x1f) == 0)
            SpawnFireball(spawns);
    }

    private void UpdateBlue(ICollection<RoomEntitySpawn> spawns)
    {
        if (_phase == 0)
        {
            _phase = 1;
            _counter = 8;
            _counter2 = 8;
            _spinDelay = (_random.Next().Value & 2) == 0 ? -2 : 2;
            SetMouthCollision(0x00);
            return;
        }

        // Only substate 1 scans the dynamic item slots for a Bomb. The open
        // mouth during substates 2/3 is not another catch window.
        if (_phase == 1 && TryBeginPendingBomb())
            return;

        if (_phase == 1)
        {
            if (--_counter != 0)
            {
                AdvanceAnimation();
                return;
            }
            _phase = 2;
            _counter = 8;
            SetMouthCollision(0x03);
            spawns.Add(new HeadThwompProjectileSpawn(
                Position + Vector2.Up * 8,
                HeadThwompProjectileKind.Circular,
                Angle: 0,
                Speed: _spinDelay));
            SetAnimation(_direction);
            return;
        }
        if (_phase == 2)
        {
            if (--_counter != 0)
            {
                AdvanceAnimation();
                return;
            }
            _counter = 30;
            _phase = 3;
            return;
        }

        if (--_counter != 0)
            return;
        _counter2--;
        if (_counter2 == 0)
        {
            SetMouthCollision(0x00);
            SetAnimation(_direction + 8);
            BeginResume(0x10);
            return;
        }
        _phase = 1;
        _counter = 8;
        SetMouthCollision(0x00);
        SetAnimation(_direction + 8);
    }

    private void UpdatePurple(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                _verticalSpeedFixed = 0x0200;
                SetSurroundingSolidity(false);
                return;
            case 1:
                Position += new Vector2(0, _verticalSpeedFixed / 256.0f);
                _verticalSpeedFixed += 0x20;
                if (Position.Y < 0x90)
                    return;
                Position = new Vector2(Position.X, 0x90);
                _phase = 2;
                _counter = 120;
                _screenShake(60);
                _playSound(OracleSoundEngine.SndStrongPound);
                return;
            case 2:
                if (--_counter == 0)
                {
                    _phase = 3;
                    return;
                }
                if (_counter >= 30 && (_counter & 0x0f) == 0)
                    spawns.Add(new HeadThwompBoulderSpawn());
                return;
            case 3:
                Position += Vector2.Up * 0.5f;
                if (Position.Y > 0x56)
                    return;
                Position = new Vector2(Position.X, 0x56);
                _phase = 4;
                return;
            case 4:
                if (Mathf.Abs(player.Position.X - Position.X) <= 16 &&
                    Mathf.Abs(player.Position.Y - Position.Y) <= 16)
                {
                    return;
                }
                SetSurroundingSolidity(true);
                BeginResume(0x10);
                return;
        }
    }

    private void UpdateRed(ICollection<RoomEntitySpawn> spawns)
    {
        if (_phase == 0)
        {
            _phase = 1;
            _counter = 120;
            Health--;
            InvincibilityCounter = 0x18;
            if (Health == 0)
                SetSurroundingSolidity(false);
            else
                spawns.Add(new ItemDropSpawn(
                    ItemDropDatabase.Heart,
                    Position + Vector2.Down * 20,
                    UpdateThisFrame: true));
            SetAnimation(0x10);
            _playSound(OracleSoundEngine.SndBossDamage);
            return;
        }
        if (Health == 0)
        {
            Position += new Vector2(0, _verticalSpeedFixed / 256.0f);
            _verticalSpeedFixed += 0x20;
            if (Position.Y >= 0x90)
            {
                Position = new Vector2(Position.X, 0x90);
                _screenShake(60);
                _playSound(OracleSoundEngine.SndStrongPound);
                // enemyCode79 observes ENEMYSTATUS_NO_HEALTH and enters the
                // shared boss-death handler on the following enemy update.
                _deathPending = true;
            }
            return;
        }
        if (--_counter == 0)
        {
            SetMouthCollision(0x00);
            SetAnimation(0x0e);
            BeginResume(0x10);
        }
    }

    private void BeginResume(int counter)
    {
        _phase = 0;
        _state = HeadThwompState.Resume;
        _counter = counter;
    }

    private bool ChecksForBombsThisUpdate() => _state switch
    {
        HeadThwompState.Spinning => true,
        HeadThwompState.Green => _phase == 1,
        HeadThwompState.Blue => _phase == 1,
        _ => false
    };

    private void UpdateBackgroundProjectiles(
        int frameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        if ((frameCounter & 1) != 0)
            return;
        _projectileCounter--;
        if (_projectileCounter == 0)
            _projectileCounter = 0xf0;
        if (_projectileCounter >= 90 ||
            (_projectileCounter & 0x0f) != 0)
        {
            return;
        }
        if ((_random.Next().Value & 7) == 0)
        {
            spawns.Add(new HeadThwompBombDropSpawn(Position));
            return;
        }
        SpawnFireball(spawns);
    }

    private void SpawnFireball(ICollection<RoomEntitySpawn> spawns)
    {
        spawns.Add(new HeadThwompProjectileSpawn(
            Position,
            HeadThwompProjectileKind.Fireball,
            Angle: 0,
            Speed: 0,
            RandomizeLaunch: true));
    }

    private void BeginDeath()
    {
        if (_dying)
            return;
        _dying = true;
        _deathCounter = 120;
        _disableLink();
        _playSound(OracleSoundEngine.SndBossDead);
    }

    private void SetSurroundingSolidity(bool solid)
    {
        SetCollision(0x46, (byte)(solid ? 0x01 : 0x00));
        if (!solid)
            SetMouthCollision(0x00);
        SetCollision(0x48, (byte)(solid ? 0x02 : 0x00));
        SetCollision(0x56, (byte)(solid ? 0x05 : 0x00));
        SetCollision(0x57, (byte)(solid ? 0x0f : 0x00));
        SetCollision(0x58, (byte)(solid ? 0x0a : 0x00));
    }

    private void SetMouthCollision(byte collision) =>
        SetCollision(0x47, collision);

    private void SetCollision(int packedPosition, byte collision)
    {
        Vector2 point = PointForPackedPosition(packedPosition);
        byte tile = _room.GetMetatile(point);
        _room.SetPositionTileAndCollision(
            point,
            tile,
            collision,
            _animationTick(),
            preserveRenderedTile: true);
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);
}

internal enum HeadThwompState
{
    WaitingForLink,
    Spinning,
    BombPause,
    FastSpin,
    Decelerating,
    FacePause,
    Green,
    Blue,
    Purple,
    Red,
    Resume
}
